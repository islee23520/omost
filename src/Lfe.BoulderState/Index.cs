using Lfe.BoulderState.Storage;

namespace Lfe.BoulderState;

public static class Index
{
    public static TopLevelTaskRef? ReadCurrentTopLevelTask(string planPath) => TopLevelTask.ReadCurrentTopLevelTask(planPath);
}
