using FormaGeo.Domain.Analyses;

namespace FormaGeo.UnitTests.Analyses;

public sealed class AnalysisRunTests
{
    [Fact]
    public void Create_InitializesPendingVersionedRun()
    {
        var analysisRun = CreateRun();

        Assert.Equal(AnalysisStatus.Pending, analysisRun.Status);
        Assert.Equal(AnalysisType.HazardExposure, analysisRun.AnalysisType);
        Assert.Equal("1.0.0", analysisRun.AnalysisVersion);
        Assert.Null(analysisRun.StartedAtUtc);
        Assert.Null(analysisRun.CompletedAtUtc);
    }

    [Fact]
    public void StartAndComplete_CaptureLifecycleDatesAndResult()
    {
        var analysisRun = CreateRun();

        analysisRun.Start();
        analysisRun.Complete(
            """{"summary":"Completed"}""");

        Assert.Equal(AnalysisStatus.Completed, analysisRun.Status);
        Assert.NotNull(analysisRun.StartedAtUtc);
        Assert.NotNull(analysisRun.CompletedAtUtc);
        Assert.Equal(
            """{"summary":"Completed"}""",
            analysisRun.ResultJson);
        Assert.Null(analysisRun.ErrorMessage);
    }

    [Fact]
    public void Fail_PreservesUsefulDiagnosticMessage()
    {
        var analysisRun = CreateRun();
        analysisRun.Start();

        analysisRun.Fail("Required hazard dataset was unavailable.");

        Assert.Equal(AnalysisStatus.Failed, analysisRun.Status);
        Assert.Equal(
            "Required hazard dataset was unavailable.",
            analysisRun.ErrorMessage);
        Assert.NotNull(analysisRun.CompletedAtUtc);
    }

    [Fact]
    public void Cancel_TerminatesPendingRunWithoutDeletingHistory()
    {
        var analysisRun = CreateRun();

        var cancelled = analysisRun.Cancel();

        Assert.True(cancelled);
        Assert.Equal(AnalysisStatus.Cancelled, analysisRun.Status);
        Assert.NotNull(analysisRun.CompletedAtUtc);
    }

    [Fact]
    public void TerminalRun_CannotBeCancelled()
    {
        var analysisRun = CreateRun();
        analysisRun.Start();
        analysisRun.Complete("{}");

        Assert.False(analysisRun.Cancel());
        Assert.Equal(AnalysisStatus.Completed, analysisRun.Status);
    }

    private static AnalysisRun CreateRun()
    {
        return AnalysisRun.Create(
            Guid.NewGuid(),
            AnalysisType.HazardExposure,
            """{"minimumOverlapPercent":0}""",
            "1.0.0");
    }
}
