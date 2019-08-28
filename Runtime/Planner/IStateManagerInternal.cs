namespace Unity.AI.Planner
{
    interface IStateManagerInternal
    {
        IStateData GetStateData(IStateKey stateKey, bool readWrite);
    }
}
