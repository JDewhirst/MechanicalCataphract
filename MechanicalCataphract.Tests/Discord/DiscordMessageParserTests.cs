using MechanicalCataphract.Discord;

namespace MechanicalCataphract.Tests.Discord;

[TestFixture]
public class DiscordMessageParserTests
{
    #region Envelope Parsing

    [Test]
    public void Parse_EnvelopeWithAllFields_ReturnsFullCommand()
    {
        var input = """
            :envelope:
            General Wellington
            5,3
            Meet me at the crossroads at dawn.
            """;

        var result = DiscordMessageParser.Parse(input);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Type, Is.EqualTo(CommandType.Envelope));
        Assert.That(result.TargetCommanderName, Is.EqualTo("General Wellington"));
        Assert.That(result.TargetLocationCol, Is.EqualTo(5));
        Assert.That(result.TargetLocationRow, Is.EqualTo(3));
        Assert.That(result.Content, Is.EqualTo("Meet me at the crossroads at dawn."));
    }

    [Test]
    public void Parse_EnvelopeWithCommanderOnly_NoLocation()
    {
        var input = """
            :envelope:
            Marshal Ney
            Advance at once with all available cavalry.
            """;

        var result = DiscordMessageParser.Parse(input);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Type, Is.EqualTo(CommandType.Envelope));
        Assert.That(result.TargetCommanderName, Is.EqualTo("Marshal Ney"));
        Assert.That(result.TargetLocationCol, Is.Null);
        Assert.That(result.TargetLocationRow, Is.Null);
        Assert.That(result.Content, Is.EqualTo("Advance at once with all available cavalry."));
    }

    [Test]
    public void Parse_EnvelopeWithLocationOnly_NoCommander()
    {
        var input = """
            :envelope:
            10,7
            Supplies are running low here.
            """;

        var result = DiscordMessageParser.Parse(input);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Type, Is.EqualTo(CommandType.Envelope));
        Assert.That(result.TargetCommanderName, Is.Null);
        Assert.That(result.TargetLocationCol, Is.EqualTo(10));
        Assert.That(result.TargetLocationRow, Is.EqualTo(7));
        Assert.That(result.Content, Is.EqualTo("Supplies are running low here."));
    }

    [Test]
    public void Parse_EnvelopeContentOnly()
    {
        var input = """
            :envelope:
            This is a broadcast message to anyone listening.
            """;

        var result = DiscordMessageParser.Parse(input);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Type, Is.EqualTo(CommandType.Envelope));
        // First non-location line is interpreted as target commander
        Assert.That(result.TargetCommanderName,
            Is.EqualTo("This is a broadcast message to anyone listening."));
    }

    [Test]
    public void Parse_EnvelopeMultilineContent()
    {
        var input = ":envelope:\nGeneral Lee\n5,3\nLine one.\nLine two.\nLine three.";

        var result = DiscordMessageParser.Parse(input);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Content, Is.EqualTo("Line one.\nLine two.\nLine three."));
    }

    [Test]
    public void Parse_EnvelopeUnicodeEmoji_Recognized()
    {
        var input = "\u2709\nCommander Test\nHello from unicode";

        var result = DiscordMessageParser.Parse(input);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Type, Is.EqualTo(CommandType.Envelope));
        Assert.That(result.TargetCommanderName, Is.EqualTo("Commander Test"));
    }

    [Test]
    public void Parse_EnvelopeEmpty_ReturnsEmptyContent()
    {
        var input = ":envelope:";

        var result = DiscordMessageParser.Parse(input);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Type, Is.EqualTo(CommandType.Envelope));
        Assert.That(result.Content, Is.EqualTo(string.Empty));
    }

    #endregion

    #region Scroll Parsing

    [Test]
    public void Parse_ScrollWithContent_ReturnsOrder()
    {
        var input = """
            :scroll:
            Move the 3rd Brigade to hex 5,7 and hold position.
            """;

        var result = DiscordMessageParser.Parse(input);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Type, Is.EqualTo(CommandType.Scroll));
        Assert.That(result.Content, Is.EqualTo("Move the 3rd Brigade to hex 5,7 and hold position."));
        Assert.That(result.TargetCommanderName, Is.Null);
        Assert.That(result.TargetLocationCol, Is.Null);
    }

    [Test]
    public void Parse_ScrollMultiline()
    {
        var input = ":scroll:\nFirst order.\nSecond order.\nThird order.";

        var result = DiscordMessageParser.Parse(input);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Type, Is.EqualTo(CommandType.Scroll));
        Assert.That(result.Content, Is.EqualTo("First order.\nSecond order.\nThird order."));
    }

    [Test]
    public void Parse_ScrollUnicodeEmoji_Recognized()
    {
        var input = "\U0001F4DC\nHold the line!";

        var result = DiscordMessageParser.Parse(input);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Type, Is.EqualTo(CommandType.Scroll));
        Assert.That(result.Content, Is.EqualTo("Hold the line!"));
    }

    [Test]
    public void Parse_ScrollEmpty_ReturnsEmptyContent()
    {
        var input = ":scroll:";

        var result = DiscordMessageParser.Parse(input);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Type, Is.EqualTo(CommandType.Scroll));
        Assert.That(result.Content, Is.EqualTo(string.Empty));
    }

    #endregion

    #region Non-Command Messages

    [Test]
    public void Parse_NormalMessage_ReturnsNull()
    {
        var result = DiscordMessageParser.Parse("Just chatting normally.");

        Assert.That(result, Is.Null);
    }

    [Test]
    public void Parse_EmptyString_ReturnsNull()
    {
        Assert.That(DiscordMessageParser.Parse(""), Is.Null);
        Assert.That(DiscordMessageParser.Parse(null!), Is.Null);
        Assert.That(DiscordMessageParser.Parse("  "), Is.Null);
    }

    [Test]
    public void Parse_EnvelopeNotOnFirstLine_ReturnsNull()
    {
        var input = "Hello\n:envelope:\nThis should not parse";

        var result = DiscordMessageParser.Parse(input);

        Assert.That(result, Is.Null, "Emoji must be on the first line to be recognized");
    }

    #endregion

    #region Location Parsing Edge Cases

    [Test]
    public void Parse_NegativeCoordinates()
    {
        var input = ":envelope:\n-3,-5\nContent here";

        var result = DiscordMessageParser.Parse(input);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.TargetLocationCol, Is.EqualTo(-3));
        Assert.That(result.TargetLocationRow, Is.EqualTo(-5));
    }

    [Test]
    public void Parse_ZeroCoordinates()
    {
        var input = ":envelope:\n0,0\nAt origin";

        var result = DiscordMessageParser.Parse(input);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.TargetLocationCol, Is.EqualTo(0));
        Assert.That(result.TargetLocationRow, Is.EqualTo(0));
    }

    [Test]
    public void Parse_InvalidCoordinates_TreatedAsCommanderName()
    {
        var input = ":envelope:\nabc,def\nContent";

        var result = DiscordMessageParser.Parse(input);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.TargetCommanderName, Is.EqualTo("abc,def"));
        Assert.That(result.TargetLocationCol, Is.Null);
    }

    #endregion
}
