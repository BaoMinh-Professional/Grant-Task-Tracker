using GrantTracker.Core.Common.Errors;
using GrantTracker.Core.Tasks;

namespace GrantTracker.Core.Tests.Tasks;

public sealed class TaskRulesTests
{
    private static readonly DateOnly Today = new(2026, 3, 10);
    private static readonly DateOnly ApplicationDate = new(2026, 2, 1);

    [Fact]
    public void TrimsTitleAndKeepsValidValues()
    {
        var valid = TaskRules.Validate("  Check  ", Today, TaskPriority.High, ApplicationDate, Today);
        Assert.Equal("Check", valid.Title);
        Assert.Equal(Today, valid.DueDate);
        Assert.Equal(TaskPriority.High, valid.Priority);
    }

    [Fact]
    public void DueDateTodayIsAccepted()
    {
        var valid = TaskRules.Validate("Check", Today, TaskPriority.Low, ApplicationDate, Today);
        Assert.Equal(Today, valid.DueDate);
    }

    [Fact]
    public void DueDateInThePastIsRejected()
    {
        var exception = Assert.Throws<ValidationException>(() =>
            TaskRules.Validate("Check", new DateOnly(2026, 3, 9), TaskPriority.Low, ApplicationDate, Today));
        Assert.Equal(ErrorCodes.DueDateInvalid, exception.Code);
        Assert.Equal(["The due date can't be in the past."], exception.Errors["dueDate"]);
    }

    [Fact]
    public void DueDateOnApplicationDateIsAccepted()
    {
        var applicationDate = new DateOnly(2026, 3, 15);
        var valid = TaskRules.Validate("Check", applicationDate, TaskPriority.Low, applicationDate, Today);
        Assert.Equal(applicationDate, valid.DueDate);
    }

    [Fact]
    public void DueDateBeforeApplicationDateIsRejectedWithOnlyThatMessage()
    {
        var exception = Assert.Throws<ValidationException>(() =>
            TaskRules.Validate("Check", new DateOnly(2026, 3, 12), TaskPriority.Low, new DateOnly(2026, 3, 15), Today));
        Assert.Equal(ErrorCodes.DueDateInvalid, exception.Code);
        Assert.Equal(["The due date can't be before the grant's application date."], exception.Errors["dueDate"]);
    }

    [Fact]
    public void DueDateInThePastAndBeforeApplicationDateReportsBothMessages()
    {
        var exception = Assert.Throws<ValidationException>(() =>
            TaskRules.Validate("Check", new DateOnly(2026, 3, 1), TaskPriority.Low, new DateOnly(2026, 3, 15), Today));
        Assert.Equal(ErrorCodes.DueDateInvalid, exception.Code);
        Assert.Equal(
            ["The due date can't be in the past.", "The due date can't be before the grant's application date."],
            exception.Errors["dueDate"]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void BlankTitleIsRejected(string? title)
    {
        var exception = Assert.Throws<ValidationException>(() =>
            TaskRules.Validate(title, Today, TaskPriority.Low, ApplicationDate, Today));
        Assert.Equal(ErrorCodes.TitleInvalid, exception.Code);
        Assert.Equal(["Enter a task title."], exception.Errors["title"]);
    }

    [Fact]
    public void TitleOfOneHundredCharactersIsAcceptedAndOneHundredAndOneIsRejected()
    {
        var hundred = new string('a', 100);
        Assert.Equal(hundred, TaskRules.Validate(hundred, Today, TaskPriority.Low, ApplicationDate, Today).Title);

        var exception = Assert.Throws<ValidationException>(() =>
            TaskRules.Validate(new string('a', 101), Today, TaskPriority.Low, ApplicationDate, Today));
        Assert.Equal(ErrorCodes.TitleInvalid, exception.Code);
        Assert.Equal(["The title must be 100 characters or fewer."], exception.Errors["title"]);
    }

    [Fact]
    public void TitleAndDueDateErrorsTogetherUseValidationFailed()
    {
        var exception = Assert.Throws<ValidationException>(() =>
            TaskRules.Validate(" ", new DateOnly(2026, 3, 9), TaskPriority.Low, ApplicationDate, Today));
        Assert.Equal(ErrorCodes.ValidationFailed, exception.Code);
        Assert.Contains("title", exception.Errors.Keys);
        Assert.Contains("dueDate", exception.Errors.Keys);
    }

    [Fact]
    public void MissingDueDateAndPriorityAreRequired()
    {
        var exception = Assert.Throws<ValidationException>(() =>
            TaskRules.Validate("Check", null, null, ApplicationDate, Today));
        Assert.Equal(ErrorCodes.ValidationFailed, exception.Code);
        Assert.Contains("dueDate", exception.Errors.Keys);
        Assert.Contains("priority", exception.Errors.Keys);
    }

    [Fact]
    public void EditModeSkipsDueDateRulesWhenNotEnforced()
    {
        var pastDue = new DateOnly(2026, 3, 1);
        var valid = TaskRules.Validate("Check", pastDue, TaskPriority.Low, ApplicationDate, Today, enforceDueDate: false);
        Assert.Equal(pastDue, valid.DueDate);
    }
}
