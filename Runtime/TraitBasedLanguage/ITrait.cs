using Unity.Entities;

namespace Unity.AI.Planner.DomainLanguage.TraitBased
{
    /// <summary>
    /// The interface denoting that the container is a trait. Base interface for <see cref="ITrait{T}"/>.
    /// </summary>
    public interface ITrait : IBufferElementData
    {
        /// <summary>
        /// Set a field on the trait (alternative to using reflection)
        /// </summary>
        /// <param name="fieldName">Name of the field</param>
        /// <param name="value">Value to set</param>
        void SetField(string fieldName, object value);
    }


    /// <summary>
    /// The interface denoting the container is a trait
    /// </summary>
    /// <typeparam name="T">Trait type</typeparam>
    public interface ITrait<T> : ITrait
    {
    }
}
