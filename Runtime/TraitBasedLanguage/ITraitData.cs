using System;

namespace Unity.AI.Planner.DomainLanguage.TraitBased
{
    /// <summary>
    /// Interface used to hold field data of a trait instance
    /// </summary>
    public interface ITraitData
    {
        /// <summary>
        /// Name of the trait definition
        /// </summary>
        string TraitDefinitionName { get; }

        /// <summary>
        /// Initialize values for all Trait fields
        /// </summary>
        void InitializeFieldValues();

        /// <summary>
        /// Try to get a value from a field
        /// </summary>
        /// <param name="fieldName">Field name</param>
        /// <param name="value">Value to be returned</param>
        /// <typeparam name="T">Value type</typeparam>
        /// <returns>Whether the value was found</returns>
        bool TryGetValue<T>(string fieldName, out T value) where T : class;

        /// <summary>
        /// Get a value from a field
        /// </summary>
        /// <param name="fieldName">Field name</param>
        /// <returns>Specified field value</returns>
        object GetValue(string fieldName);

        /// <summary>
        /// Set a value to a field
        /// </summary>
        /// <param name="fieldName">Field name</param>
        /// <param name="value">Value</param>
        void SetValue(string fieldName, object value);
    }
}
