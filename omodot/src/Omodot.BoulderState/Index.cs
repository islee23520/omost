using Omodot.BoulderState.Storage;

namespace Omodot.BoulderState;

public static class Index
{
    public static TopLevelTaskRef? ReadCurrentTopLevelTask(string planPath) => TopLevelTask.ReadCurrentTopLevelTask(planPath);
}
