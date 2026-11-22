using CoreUtils;
using UnityEngine;
using UnityEngine.UIElements;

namespace NavalCombat.Unity;

public sealed class NavalLeaderPortraitView : IDisposable
{
    public VisualElement Root { get; } = new();
    public DropdownField Mode { get; }
    public bool ReserveSpace => leader != null && Mode.index == 0;
    public event Action<Leader> DisplayedLeaderChanged;

    readonly NavalLeaderPortraitCache cache;
    readonly Image image = new() { scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore };
    readonly VisualElement caption = new() { pickingMode = PickingMode.Ignore };
    readonly VisualElement navigation = new();
    readonly Label name = new(), position = new();
    NavalLeaderPortraitCache.Entry entry;
    Leader leader;

    public NavalLeaderPortraitView(MonoBehaviour host, Action previous, Action next)
    {
        cache = host.GetComponent<NavalLeaderPortraitCache>() ?? host.gameObject.AddComponent<NavalLeaderPortraitCache>();
        Root.AddToClassList("leader-portrait");
        image.AddToClassList("leader-image");
        Root.Add(image);
        caption.AddToClassList("leader-caption");
        position.AddToClassList("leader-position");
        name.AddToClassList("leader-name");
        caption.Add(position);
        caption.Add(name);
        Root.Add(caption);
        navigation.AddToClassList("leader-navigation");
        navigation.Add(new Button(previous) { text = "‹", name = "previousLeader" });
        navigation.Add(new Button(next) { text = "›", name = "nextLeader" });
        Root.Add(navigation);
        Mode = new DropdownField(new List<string> { "Photo", "Hidden" }, 0);
        Mode.AddToClassList("leader-mode");
        Root.Add(Mode);
        Mode.RegisterValueChangedCallback(_ => Refresh());
    }

    public void Localize(string photo, string hidden, string tooltip, string previous, string next)
    {
        int index = Mode.index;
        Mode.choices = new() { photo, hidden };
        Mode.SetValueWithoutNotify(Mode.choices[Mathf.Clamp(index, 0, 1)]);
        Mode.tooltip = tooltip;
        navigation.Q<Button>("previousLeader").tooltip = previous;
        navigation.Q<Button>("nextLeader").tooltip = next;
    }

    public void SetLeader(Leader value, string role, bool canSwitch)
    {
        bool changed = leader != value;
        leader = value;
        name.text = leader?.name?.GetMergedName() ?? "";
        position.text = role ?? "";
        Root.tooltip = position.text + "\n" + name.text;
        navigation.style.display = canSwitch ? DisplayStyle.Flex : DisplayStyle.None;
        if (changed)
        {
            Refresh();
            DisplayedLeaderChanged?.Invoke(leader);
        }
    }

    void Refresh()
    {
        bool visible = ReserveSpace;
        var reference = leader?.portraitReference;
        var requested = visible && !string.IsNullOrWhiteSpace(reference?.path) ? cache.Get(reference) : null;
        if (entry != requested)
        {
            if (entry != null) entry.Loaded -= ShowImage;
            entry = requested;
            if (entry != null) entry.Loaded += ShowImage;
        }
        ShowImage();
        caption.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        Root.EnableInClassList("leader-collapsed", !visible);
    }

    void ShowImage()
    {
        image.image = entry?.Texture;
        image.style.display = image.image != null ? DisplayStyle.Flex : DisplayStyle.None;
    }

    public void Dispose()
    {
        if (entry != null) entry.Loaded -= ShowImage;
        image.image = null;
    }
}
