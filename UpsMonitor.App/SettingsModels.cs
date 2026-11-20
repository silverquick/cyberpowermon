using System.Windows.Input;
using UpsMonitor.Core;
namespace UpsMonitor.App;
public sealed record LanguageOption(string Code, string DisplayName)
{
    public override string ToString() => DisplayName;
}

public sealed record OfficialRecordRow(
    OfficialHealthRecord Record,
    string DateText,
    string ValueText,
    string PredictedText,
    string ErrorText,
    string DetailText,
    ICommand DeleteCommand)
{
    public bool HasDetail => DetailText.Length > 0;
}

public sealed record OfficialModelRow(
    string Name,
    string ValueText,
    string CountText,
    string MaeText,
    string SelectedText);
