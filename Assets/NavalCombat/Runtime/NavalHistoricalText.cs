using UnityEngine;
using UnityEngine.UIElements;
namespace NavalCombat.Unity
{
public sealed partial class NavalGame
{
    void AddHistoricalText(VisualElement host,string source)
    {
        var renderer=new MarkdownRenderer{value=source,selectable=true};host.Add(renderer);
        ActionButton(host,L("CopyMarkdownSource"),()=>GUIUtility.systemCopyBuffer=source);
    }
}
}
