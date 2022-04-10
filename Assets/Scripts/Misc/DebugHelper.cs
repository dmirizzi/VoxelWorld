using System.Linq;
using System.Text;

class DebugHelper
{
    public static string GetPropertiesString(object obj)
    {
        return obj.GetType().GetProperties()
            .Select(info => (info.Name, Value: info.GetValue(obj, null) ?? "(null)"))
            .Aggregate(
                new StringBuilder(),
                (sb, pair) => sb.AppendLine($"{pair.Name}: {pair.Value}"),
                sb => sb.ToString());
    }
}