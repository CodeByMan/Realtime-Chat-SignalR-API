using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using RealTimeChatAPI.Data.Repositories;
using RealTimeChatAPI.DTOs;
using RealTimeChatAPI.Exceptions;
using RealTimeChatAPI.Models;
using System.Text.Json;

namespace RealTimeChatAPI.Hubs;

[Authorize]
public class ChatHub(
        IUsersRepository usersRepository,
        IMessagesRepository messagesRepository,
        IMapper mapper,
        UserConnectionManager connectionManager,
        ILogger<ChatHub> logger) : Hub
{
    private const int MaximumMessageLength = 4000;

    public override async Task OnConnectedAsync()
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            Context.Abort();
            return;
        }

        connectionManager.Add(userId, Context.ConnectionId);

        try
        {
            await DeliveredAllMessages(userId);
            await base.OnConnectedAsync();
        }
        catch
        {
            connectionManager.Remove(userId, Context.ConnectionId);
            throw;
        }
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (TryGetCurrentUserId(out var userId))
            connectionManager.Remove(userId, Context.ConnectionId);

        await base.OnDisconnectedAsync(exception);
    }

    public async Task SendMessage(string userId, string message)
    {
        try
        {
            if (!Guid.TryParse(userId, out var recipientId))
                throw new BadRequestException("Invalid user ID.");

            if (string.IsNullOrWhiteSpace(message))
                throw new BadRequestException("Message cannot be empty.");

            if (message.Length > MaximumMessageLength)
                throw new BadRequestException($"Message cannot exceed {MaximumMessageLength} characters.");

            var recipient = await usersRepository.GetByIdAsync(recipientId)
                ?? throw new NotFoundException(nameof(User), userId);

            var senderId = GetCurrentUserId();

            var savedMessage = await messagesRepository.AddAsync(new Message
            {
                SenderId = senderId,
                RecipientId = recipient.Id,
                Content = message
            });

            var jsonMessage = JsonSerializer.Serialize(mapper.Map<MessageDto>(savedMessage));

            await Clients.Caller.SendAsync("SendMessage", jsonMessage);
            await SendToUserConnections(recipient.Id, "ReceiveMessage", jsonMessage);
        }
        catch (Exception ex)
        {
            await SendSafeError("SendMessage", ex);
        }
    }

    public async Task DeliveredMessage(string messageId)
    {
        try
        {
            if (!Guid.TryParse(messageId, out var parsedMessageId))
                throw new BadRequestException("Invalid message ID.");

            var message = await messagesRepository.GetByIdAsync(parsedMessageId)
                ?? throw new NotFoundException(nameof(Message), messageId);

            var userId = GetCurrentUserId();
            if (message.RecipientId != userId)
                return;

            message.DeliveredAt ??= DateTime.UtcNow;

            var updatedMessage = await messagesRepository.UpdateAsync(message);
            var jsonMessage = "[" + JsonSerializer.Serialize(mapper.Map<MessageDto>(updatedMessage)) + "]";

            await SendToUserConnections(message.SenderId, "MessageStatus", jsonMessage);
        }
        catch (Exception ex)
        {
            await SendSafeError("DeliveredMessage", ex);
        }
    }

    public async Task ReadMessages(string userChatId)
    {
        try
        {
            if (!Guid.TryParse(userChatId, out var chatUserId))
                throw new BadRequestException("Invalid chat user ID.");

            var chatUser = await usersRepository.GetByIdAsync(chatUserId)
                ?? throw new NotFoundException(nameof(User), userChatId);

            var currentUserId = GetCurrentUserId();
            var messages = await messagesRepository.ReadUnreadMessagesAsync(currentUserId, chatUserId);
            var jsonMessages = JsonSerializer.Serialize(mapper.Map<IEnumerable<MessageDto>>(messages));

            await SendToUserConnections(chatUser.Id, "MessageStatus", jsonMessages);
        }
        catch (Exception ex)
        {
            await SendSafeError("ReadMessages", ex);
        }
    }

    public async Task DeliveredAndReadMessage(string messageId)
    {
        try
        {
            if (!Guid.TryParse(messageId, out var parsedMessageId))
                throw new BadRequestException("Invalid message ID.");

            var message = await messagesRepository.GetByIdAsync(parsedMessageId)
                ?? throw new NotFoundException(nameof(Message), messageId);

            var userId = GetCurrentUserId();
            if (message.RecipientId != userId)
                return;

            var now = DateTime.UtcNow;
            message.DeliveredAt ??= now;
            message.ReadAt ??= now;

            var updatedMessage = await messagesRepository.UpdateAsync(message);
            var jsonMessage = "[" + JsonSerializer.Serialize(mapper.Map<MessageDto>(updatedMessage)) + "]";

            await SendToUserConnections(message.SenderId, "MessageStatus", jsonMessage);
        }
        catch (Exception ex)
        {
            await SendSafeError("DeliveredAndReadMessage", ex);
        }
    }

    private async Task DeliveredAllMessages(Guid userId)
    {
        var messages = await messagesRepository.DeliveredAllMessagesAsync(userId);

        foreach (var group in messages.GroupBy(message => message.SenderId))
        {
            var jsonMessages = JsonSerializer.Serialize(mapper.Map<IEnumerable<MessageDto>>(group));
            await SendToUserConnections(group.Key, "MessageStatus", jsonMessages);
        }
    }

    private async Task SendToUserConnections(Guid userId, string method, string payload)
    {
        var connectionIds = connectionManager.GetConnections(userId);
        if (connectionIds.Count > 0)
            await Clients.Clients(connectionIds.ToArray()).SendAsync(method, payload);
    }

    private async Task SendSafeError(string operation, Exception exception)
    {
        if (exception is BadRequestException or NotFoundException)
        {
            await Clients.Caller.SendAsync("Error", exception.Message);
            return;
        }

        logger.LogError(exception,
            "Unexpected SignalR error during {Operation} for connection {ConnectionId}",
            operation,
            Context.ConnectionId);

        await Clients.Caller.SendAsync("Error", "An unexpected error occurred.");
    }

    private Guid GetCurrentUserId()
    {
        if (!TryGetCurrentUserId(out var userId))
            throw new UnauthorizedAccessException("Authenticated user ID is missing or invalid.");

        return userId;
    }

    private bool TryGetCurrentUserId(out Guid userId)
    {
        userId = Guid.Empty;
        return Context.User?.Identity?.IsAuthenticated == true &&
            Guid.TryParse(Context.UserIdentifier, out userId);
    }
}
