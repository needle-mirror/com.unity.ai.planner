namespace Unity.AI.Planner.DomainLanguage.TraitBased
{
    /// <summary>
    /// Interface used in conjunction with <see cref="IActionKey"/> to report the name of an action
    /// </summary>
    public interface IGetActionName
    {
        /// <summary>
        /// Get the name for the action
        /// </summary>
        /// <param name="actionKey">Key for accessing the action</param>
        /// <returns>A label for the action</returns>
        string GetActionName(IActionKey actionKey);
    }
}
