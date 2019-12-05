using Unity.Entities;

namespace Unity.AI.Planner.DomainLanguage.TraitBased
{
    /// <summary>
    /// The interface denoting that the container is a trait.
    /// </summary>
    public interface ITrait : IBufferElementData
    {
        /// <summary>
        /// Get the value of a field
        /// </summary>
        /// <param name="fieldName">Name of field</param>
        /// <returns>Field value if field exists</returns>
        object GetField(string fieldName);

        /// <summary>
        /// Set a field on the trait (alternative to using reflection)
        /// </summary>
        /// <param name="fieldName">Name of the field</param>
        /// <param name="value">Value to set</param>
        void SetField(string fieldName, object value);
    }
}
