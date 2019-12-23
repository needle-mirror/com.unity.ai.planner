using UnityEditor;
using UnityEngine;

class PlannerResources : ScriptableSingleton<PlannerResources>
{
    public Texture2D ImageRequiredTraitLabel => m_ImageRequiredTraitLabel;
    public Texture2D ImageRequiredTraitAdd => m_ImageRequiredTraitAdd;
    public Texture2D ImageRequiredTraitMore => m_ImageRequiredTraitMore;
    public Texture2D ImageProhibitedTraitLabel => m_ImageProhibitedTraitLabel;
    public Texture2D ImageProhibitedTraitAdd => m_ImageProhibitedTraitAdd;
    public Texture2D ImageProhibitedTraitMore => m_ImageProhibitedTraitMore;

    public TextAsset TemplateAction => m_TemplateAction;
    public TextAsset TemplateActionScheduler => m_TemplateActionScheduler;
    public TextAsset TemplateDomain => m_TemplateDomain;
    public TextAsset TemplateEnum => m_TemplateEnum;
    public TextAsset TemplateTermination => m_TemplateTermination;
    public TextAsset TemplateTrait => m_TemplateTrait;
    public TextAsset TemplatePlanExecutor => m_TemplatePlanExecutor;
    public TextAsset TemplatePackage => m_TemplatePackage;
    public TextAsset TemplateDomainsAsmDef => m_TemplateDomainsAsmDef;
    public TextAsset TemplateActionsAsmDef => m_TemplateActionsAsmDef;

#pragma warning disable 0649

    [SerializeField]
    Texture2D m_ImageRequiredTraitLabel;
    [SerializeField]
    Texture2D m_ImageRequiredTraitAdd;
    [SerializeField]
    Texture2D m_ImageRequiredTraitMore;
    [SerializeField]
    Texture2D m_ImageProhibitedTraitLabel;
    [SerializeField]
    Texture2D m_ImageProhibitedTraitAdd;
    [SerializeField]
    Texture2D m_ImageProhibitedTraitMore;

    [SerializeField]
    TextAsset m_TemplateAction;
    [SerializeField]
    TextAsset m_TemplateActionScheduler;
    [SerializeField]
    TextAsset m_TemplateDomain;
    [SerializeField]
    TextAsset m_TemplateEnum;
    [SerializeField]
    TextAsset m_TemplateTermination;
    [SerializeField]
    TextAsset m_TemplateTrait;
    [SerializeField]
    TextAsset m_TemplatePlanExecutor;
    [SerializeField]
    TextAsset m_TemplatePackage;
    [SerializeField]
    TextAsset m_TemplateDomainsAsmDef;
    [SerializeField]
    TextAsset m_TemplateActionsAsmDef;

#pragma warning restore 0649
}
