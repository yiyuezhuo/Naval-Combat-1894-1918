namespace NavalCombat.Avalonia.Resources
{
    public static class NavalCombatStrings
    {
        public static string Text(string key, params object[] args)=>NavalCombat.Unity.NavalText.Instance.Text(key,args);
        public static string GetCore(string key, params object[] args)=>NavalCombat.Unity.NavalText.Instance.Get(key,args);
        public static string GetUi(string key, params object[] args)=>Text("Ui."+key,args);
        public static string EnumLabel(Enum value)=>GetCore(value.GetType().Name+"."+value);
    }
}
namespace System.Linq
{
    internal static class NavalPresentationCompatibility
    {
        public static T NavalMaxBy<T>(this IEnumerable<T> items,Func<T,double> score) where T:class
        {
            T best=null;double maximum=double.NegativeInfinity;
            foreach(var item in items)if(best==null || score(item)>maximum){best=item;maximum=score(item);}
            return best;
        }
    }
}
