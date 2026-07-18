using FluentValidation;

namespace RealTimeChatAPI.Services.Messages.Queries.GetMessages;

public class GetMessagesQueryValidator : AbstractValidator<GetMessagesQuery>
{
    public GetMessagesQueryValidator()
    {
        RuleFor(query => query.UserId).NotEmpty();
    }
}
