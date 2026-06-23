namespace RomForge.ViewModels.Patch;

public interface IPatchViewModel
{
    string? SourcePath { get; }

    Task RunAsync();

    void Cancel();

    void Clear();
}