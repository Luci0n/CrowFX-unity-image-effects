
using System;

[AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
public sealed class EffectSectionAttribute : Attribute
{
    public string Section { get; }
    public int Order { get; }

    public EffectSectionAttribute(string section, int order = 0)
    {
        Section = section ?? "Misc";
        Order = order;
    }
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
public sealed class EffectSectionMetaAttribute : Attribute
{
    public string Key { get; }
    public string Title { get; }
    public string Icon { get; }
    public string Hint { get; }
    public int Order { get; }
    public bool DefaultExpanded { get; }

    public EffectSectionMetaAttribute(
        string key,
        string title = null,
        string icon = "d_Settings",
        string hint = null,
        int order = 0,
        bool defaultExpanded = false)
    {
        Key = key ?? "Misc";
        Title = string.IsNullOrEmpty(title) ? Key : title;
        Icon = string.IsNullOrEmpty(icon) ? "d_Settings" : icon;
        Hint = hint ?? "";
        Order = order;
        DefaultExpanded = defaultExpanded;
    }
}
