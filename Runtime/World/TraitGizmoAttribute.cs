using System;

namespace UnityEngine.AI.Planner.DomainLanguage.TraitBased
{

    /// <summary>
    /// EXPERIMENTAL: An attribute type for drawing gizmos on game objects with a given trait.
    /// </summary>
    public class TraitGizmoAttribute : Attribute
    {
        internal Type m_TraitType;

        /// <summary>
        /// Constructs a TraitGizmoAttribute.
        /// </summary>
        /// <param name="traitType">The type of trait for the associated gizmo.</param>
        public TraitGizmoAttribute(Type traitType)
        {
            m_TraitType = traitType;
        }
    }
}
