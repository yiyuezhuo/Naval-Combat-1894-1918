using NavalCombat.Application;
using NavalCombatCore;
using UnityEngine.UIElements;

namespace NavalCombat.Unity
{
public sealed partial class NavalGame
{
    void OpenDoctrineDialog(IShipGroupMember member)
    {
        bool CanEdit()=>Session?.Phase==BattlePhase.Running && Session.IsPlayerMember(member);
        var doctrine=member.doctrine;
        var body=new ScrollView{name="doctrineEditor"};
        body.AddToClassList("grow");
        var changes=new List<Action>();
        var validNumbers=new List<Func<bool>>();
        var inherit=U("Ui.Inherit");
        var specified=U("Ui.Specified");
        var unlimited=Message("Unlimited","無制限","不限","不限");
        string InheritValue(string value)=>L("InheritedValue",value);
        DropdownField Choice(string key,List<string> options,int index)
        {
            var control=new DropdownField(U("Ui."+key),options,index);
            body.Add(control);
            return control;
        }
        void Mode<T>(string key,Inheriable<T> setting,T effective,T first,T second,Func<T,string> format) where T:new()
        {
            var field=Choice(key,new(){setting.isInherited?InheritValue(format(effective)):inherit,format(first),format(second)},
                setting.isInherited?0:EqualityComparer<T>.Default.Equals(setting.value,first)?1:2);
            changes.Add(()=>
            {
                setting.isInherited=field.index==0;
                if(!setting.isInherited)setting.value=field.index==1?first:second;
            });
        }
        void Automatic(string key,Inheriable<AutomaticType> setting,AutomaticType effective)
            =>Mode(key,setting,effective,AutomaticType.Manual,AutomaticType.Automatic,v=>NavalText.Instance.GetEnum(v));
        void Flag(string key,Inheriable<bool> setting,bool effective)
            =>Mode(key,setting,effective,false,true,v=>U(v?"Ui.Enabled":"Ui.Disabled"));
        void Range(string key,InheriableUnspecifiableFloat setting,UnspecifiableFloat effective)
        {
            string inherited=setting.isInherited?InheritValue(effective.isSpecified?effective.value.ToString("G"):unlimited):inherit;
            var mode=Choice(key,new(){inherited,unlimited,specified},setting.isInherited?0:setting.value.isSpecified?2:1);
            var number=new DoubleField{value=setting.isInherited?effective.value:setting.value.value};
            body.Add(number);
            void ShowNumber()=>number.style.display=mode.index==2?DisplayStyle.Flex:DisplayStyle.None;
            mode.RegisterValueChangedCallback(_=>ShowNumber());
            ShowNumber();
            validNumbers.Add(()=>mode.index!=2 || (double.IsFinite(number.value) && number.value>=0));
            changes.Add(()=>
            {
                setting.isInherited=mode.index==0;
                if(setting.isInherited)return;
                setting.value.isSpecified=mode.index==2;
                if(setting.value.isSpecified)setting.value.value=number.value;
            });
        }

        Automatic("Automatic Maneuver:",doctrine.maneuverAutomaticType,doctrine.GetManeuverAutomaticType());
        Automatic("Automatic Fire:",doctrine.fireAutomaticType,doctrine.GetFireAutomaticType());
        Automatic("Searchlight Control:",doctrine.searchlightAutomaticType,doctrine.GetSearchlightAutomaticType());
        Flag("Ammunition Fallback:",doctrine.ammunitionFallbackable,doctrine.GetAmmunitionFallbackable());
        Automatic("Ammunition Switch:",doctrine.ammunitionSwitchAutomaticType,doctrine.GetAmmunitionSwitchAutomaticType());
        Range("Max Fire Dist 200mm+ (yards):",doctrine.maximumFiringDistanceYardsFor200mmPlus,doctrine.GetMaximumFiringDistanceYardsFor200mmPlus());
        Range("Max Fire Dist 100-200mm (yards):",doctrine.maximumFiringDistanceYardsFor100mmTo200mm,doctrine.GetMaximumFiringDistanceYardsFor100mmTo200mm());
        Range("Max Fire Dist 100- (yards):",doctrine.maximumFiringDistanceYardsFor100mmLess,doctrine.GetMaximumFiringDistanceYardsFor100mmLess());
        Range("Max Fire Dist Torpedo (yards):",doctrine.maximumFiringDistanceYardsForTorpedo,doctrine.GetMaximumFiringDistanceYardsForTorpedo());
        if(debugMode)
        {
            Flag("Awaking:",doctrine.awaking,doctrine.GetAwaking());
            var countdown=doctrine.awakeCountdownMinutes;
            var mode=Choice("Awake Countdown Minutes:",new(){countdown.isInherited?InheritValue(doctrine.GetAwakeCountdownMinutes().ToString()):inherit,specified},countdown.isInherited?0:1);
            var minutes=new IntegerField{name="awakeCountdownMinutes",value=doctrine.GetAwakeCountdownMinutes()};
            body.Add(minutes);
            void ShowMinutes()=>minutes.style.display=mode.index==1?DisplayStyle.Flex:DisplayStyle.None;
            mode.RegisterValueChangedCallback(_=>ShowMinutes());
            ShowMinutes();
            validNumbers.Add(()=>mode.index==0 || minutes.value>=0);
            changes.Add(()=>
            {
                countdown.isInherited=mode.index==0;
                if(!countdown.isInherited)countdown.value=minutes.value;
            });
        }

        string kind=member is ShipGroup?Message("Formation","編制","编制","編制"):Message("Unit","ユニット","单位","單位");
        var page=PushDialog(U("Ui.Doctrine")+" · "+kind+" · "+member.GetMemberName(),body,size:"medium");
        if(!CanEdit())
        {
            foreach(var field in body.contentContainer.Children())field.SetEnabled(false);
            return;
        }
        var error=Text("","validation-error");
        body.Add(error);
        ActionButton(page.Actions,L("Apply"),()=>
        {
            if(!CanEdit())return;
            if(validNumbers.Any(valid=>!valid()))
            {
                error.text=Message("Enter a finite, non-negative value.","0 以上の有限値を入力してください。","请输入大于或等于零的有限数值。","請輸入大於或等於零的有限數值。");
                body.ScrollTo(error);
                return;
            }
            foreach(var change in changes)change();
            CloseDialog();
        }).name="applyDoctrine";
    }
}
}
