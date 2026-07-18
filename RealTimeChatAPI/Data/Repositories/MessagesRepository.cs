using Microsoft.EntityFrameworkCore;
using RealTimeChatAPI.DTOs;
using RealTimeChatAPI.Models;

namespace RealTimeChatAPI.Data.Repositories;

internal class MessagesRepository(RealTimeChatDbContext dbContext) : IMessagesRepository
{
    public async Task<Message> AddAsync(Message message)
    {
        dbContext.Messages.Add(message);
        await dbContext.SaveChangesAsync();
        return message;
    }

    public async Task<Message?> GetByIdAsync(Guid id)
    {
        return await dbContext.Messages.SingleOrDefaultAsync(m => m.Id == id);
    }

    public async Task<Message> UpdateAsync(Message message)
    {
        dbContext.Update(message);
        await dbContext.SaveChangesAsync();
        return message;
    }

    public async Task<IEnumerable<Message>> GetMessagesAsync(Guid userId1, Guid userId2)
    {
        return await dbContext.Messages
            .Where(m =>
                (m.SenderId == userId1 && m.RecipientId == userId2) ||
                (m.SenderId == userId2 && m.RecipientId == userId1))
            .OrderBy(m => m.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<ChatRoomDto>> GetChatRoomsAsync(Guid userId)
    {
        var chatRooms = await dbContext.Messages
            .Where(m => m.SenderId == userId || m.RecipientId == userId)
            .GroupBy(m => m.SenderId == userId ? m.RecipientId : m.SenderId)
            .Select(group => new
            {
                OtherParticipantId = group.Key,
                LastMessage = group.OrderByDescending(m => m.CreatedAt)
                    .Select(m => m.Content)
                    .First(),
                LastMessageTime = group.Max(m => m.CreatedAt),
                UnreadMessagesCount = group.Count(m =>
                    m.RecipientId == userId && m.ReadAt == null)
            })
            .ToListAsync();

        var participantIds = chatRooms.Select(room => room.OtherParticipantId).ToList();
        var participants = await dbContext.Users
            .Where(user => participantIds.Contains(user.Id))
            .ToDictionaryAsync(user => user.Id);

        return chatRooms
            .Where(room => participants.ContainsKey(room.OtherParticipantId))
            .Select(room =>
            {
                var participant = participants[room.OtherParticipantId];
                return new ChatRoomDto
                {
                    UserId = participant.Id,
                    Username = participant.Username,
                    Name = participant.Name,
                    Image = participant.Image,
                    LastMessage = room.LastMessage,
                    LastMessageTime = room.LastMessageTime,
                    UnreadMessagesCount = room.UnreadMessagesCount
                };
            })
            .ToList();
    }

    public async Task<IEnumerable<Message>> ReadUnreadMessagesAsync(Guid userId, Guid userChatId)
    {
        var messages = await dbContext.Messages
            .Where(m => m.RecipientId == userId && m.SenderId == userChatId && m.ReadAt == null)
            .ToListAsync();

        var now = DateTime.UtcNow;
        foreach (var message in messages)
        {
            message.DeliveredAt ??= now;
            message.ReadAt = now;
        }

        dbContext.Messages.UpdateRange(messages);
        await dbContext.SaveChangesAsync();
        return messages;
    }

    public async Task<IEnumerable<Message>> DeliveredAllMessagesAsync(Guid userId)
    {
        var messages = await dbContext.Messages
            .Where(m => m.RecipientId == userId && m.DeliveredAt == null)
            .ToListAsync();

        var now = DateTime.UtcNow;
        messages.ForEach(message => message.DeliveredAt = now);

        dbContext.Messages.UpdateRange(messages);
        await dbContext.SaveChangesAsync();
        return messages;
    }
}
