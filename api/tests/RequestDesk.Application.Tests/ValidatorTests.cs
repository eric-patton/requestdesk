using FluentValidation;
using FluentValidation.TestHelper;
using RequestDesk.Application.Common.Behaviors;
using RequestDesk.Application.Requests;
using RequestDesk.Application.Requests.Commands;
using RequestDesk.Application.Requests.Queries;
using RequestDesk.Domain.Requests;

namespace RequestDesk.Application.Tests;

public class CreateRequestCommandValidatorTests
{
    private static CreateRequestCommand Valid(Guid? customerId = null) =>
        new("Printer jamming", "Rear tray, every second page.", RequestPriority.Normal, customerId);

    [Fact]
    public void Staff_must_name_a_customer()
    {
        var validator = new CreateRequestCommandValidator(Fakes.CurrentUser(Fakes.Agent));

        validator.TestValidate(Valid()).ShouldHaveValidationErrorFor(c => c.CustomerId);
        validator.TestValidate(Valid(Guid.NewGuid())).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void A_customer_does_not_have_to_name_a_customer()
    {
        var validator = new CreateRequestCommandValidator(Fakes.CurrentUser(Fakes.CustomerA));

        validator.TestValidate(Valid()).ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Title_is_required(string title)
    {
        var validator = new CreateRequestCommandValidator(Fakes.CurrentUser(Fakes.CustomerA));

        validator.TestValidate(Valid() with { Title = title }).ShouldHaveValidationErrorFor(c => c.Title);
    }

    [Fact]
    public void Title_and_description_share_the_domain_limits()
    {
        var validator = new CreateRequestCommandValidator(Fakes.CurrentUser(Fakes.CustomerA));

        validator.TestValidate(Valid() with { Title = new string('x', RequestLimits.MaxTitleLength) }).ShouldNotHaveValidationErrorFor(c => c.Title);
        validator.TestValidate(Valid() with { Title = new string('x', RequestLimits.MaxTitleLength + 1) }).ShouldHaveValidationErrorFor(c => c.Title);
        validator.TestValidate(Valid() with { Description = new string('x', RequestLimits.MaxDescriptionLength + 1) }).ShouldHaveValidationErrorFor(c => c.Description);
    }

    [Fact]
    public void Priority_must_be_a_known_value()
    {
        var validator = new CreateRequestCommandValidator(Fakes.CurrentUser(Fakes.CustomerA));

        validator.TestValidate(Valid() with { Priority = (RequestPriority)42 }).ShouldHaveValidationErrorFor(c => c.Priority);
    }
}

public class AddAttachmentCommandValidatorTests
{
    private readonly AddAttachmentCommandValidator _validator = new();

    private static AddAttachmentCommand Valid(string contentType = "image/png", long size = 1024) =>
        new(Guid.NewGuid(), "photo.png", contentType, size, Stream.Null);

    [Theory]
    [InlineData("image/png")]
    [InlineData("image/jpeg")]
    [InlineData("application/pdf")]
    [InlineData("text/plain")]
    [InlineData("text/plain; charset=utf-8")]
    [InlineData("IMAGE/PNG")]
    public void Allowlisted_content_types_pass(string contentType)
    {
        _validator.TestValidate(Valid(contentType)).ShouldNotHaveValidationErrorFor(c => c.ContentType);
    }

    [Theory]
    [InlineData("application/x-msdownload")]
    [InlineData("application/octet-stream")]
    [InlineData("text/html")]
    [InlineData("application/javascript")]
    [InlineData("")]
    public void Anything_else_is_refused(string contentType)
    {
        _validator.TestValidate(Valid(contentType)).ShouldHaveValidationErrorFor(c => c.ContentType);
    }

    [Fact]
    public void Size_is_capped_and_empty_files_are_refused()
    {
        _validator.TestValidate(Valid(size: AttachmentRules.MaxSizeBytes)).ShouldNotHaveValidationErrorFor(c => c.SizeBytes);
        _validator.TestValidate(Valid(size: AttachmentRules.MaxSizeBytes + 1)).ShouldHaveValidationErrorFor(c => c.SizeBytes);
        _validator.TestValidate(Valid(size: 0)).ShouldHaveValidationErrorFor(c => c.SizeBytes);
    }
}

public class ListRequestsQueryValidatorTests
{
    private readonly ListRequestsQueryValidator _validator = new();

    [Fact]
    public void Page_size_is_bounded()
    {
        _validator.TestValidate(new ListRequestsQuery(PageSize: 0)).ShouldHaveValidationErrorFor(q => q.PageSize);
        _validator.TestValidate(new ListRequestsQuery(PageSize: ListRequestsQuery.MaxPageSize + 1)).ShouldHaveValidationErrorFor(q => q.PageSize);
        _validator.TestValidate(new ListRequestsQuery(PageSize: ListRequestsQuery.MaxPageSize)).ShouldNotHaveValidationErrorFor(q => q.PageSize);
        _validator.TestValidate(new ListRequestsQuery(Page: 0)).ShouldHaveValidationErrorFor(q => q.Page);
    }

    [Fact]
    public void Only_known_fields_are_sortable_case_insensitively()
    {
        _validator.TestValidate(new ListRequestsQuery(SortBy: "createdAt")).ShouldNotHaveValidationErrorFor(q => q.SortBy);
        _validator.TestValidate(new ListRequestsQuery(SortBy: "CREATEDAT")).ShouldNotHaveValidationErrorFor(q => q.SortBy);
        _validator.TestValidate(new ListRequestsQuery(SortBy: "description")).ShouldHaveValidationErrorFor(q => q.SortBy);
    }

    [Fact]
    public void Unknown_statuses_and_priorities_are_refused()
    {
        _validator.TestValidate(new ListRequestsQuery(Statuses: [(RequestStatus)99])).IsValid.ShouldBeFalse();
        _validator.TestValidate(new ListRequestsQuery(Priorities: [(RequestPriority)99])).IsValid.ShouldBeFalse();
    }
}

public class ValidationBehaviorTests
{
    private sealed record Ping(string Name) : MediatR.IRequest<string>;

    private sealed class PingValidator : AbstractValidator<Ping>
    {
        public PingValidator() => RuleFor(p => p.Name).NotEmpty();
    }

    private sealed class PingLengthValidator : AbstractValidator<Ping>
    {
        public PingLengthValidator() => RuleFor(p => p.Name).MaximumLength(3);
    }

    [Fact]
    public async Task Runs_every_validator_and_aggregates_the_failures()
    {
        var behavior = new ValidationBehavior<Ping, string>([new PingValidator(), new PingLengthValidator()]);

        var ex = await Should.ThrowAsync<ValidationException>(() =>
            behavior.Handle(new Ping("too long"), _ => Task.FromResult("handled"), CancellationToken.None));

        ex.Errors.Select(e => e.PropertyName).ShouldAllBe(p => p == "Name");
        ex.Errors.Count().ShouldBe(1);
    }

    [Fact]
    public async Task Calls_the_handler_when_everything_passes_or_when_there_are_no_validators()
    {
        var withValidators = new ValidationBehavior<Ping, string>([new PingValidator(), new PingLengthValidator()]);
        var withoutValidators = new ValidationBehavior<Ping, string>([]);

        (await withValidators.Handle(new Ping("ok"), _ => Task.FromResult("handled"), CancellationToken.None)).ShouldBe("handled");
        (await withoutValidators.Handle(new Ping(""), _ => Task.FromResult("handled"), CancellationToken.None)).ShouldBe("handled");
    }
}
