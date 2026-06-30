namespace ApiGateway.Util
{
    public static class General
    {
        public static T GetObject<T>(this Dictionary<string, object> dict)
        {
            Type type = typeof(T);
            var obj = Activator.CreateInstance(type);
            foreach (var kv in dict)
            {
                var prop = type.GetProperty(kv.Key);
                if (prop == null) continue;
                object value = kv.Value;
                if (kv.Value.GetType() == typeof(Dictionary<string, object>))
                {
                    value = GetObject<Dictionary<string, object>>((Dictionary<string, object>)value); // <= Recursive call
                }
                value = Convert.ChangeType(value, prop.PropertyType);
                prop.SetValue(obj, value, null);
            }
            return (T)obj;
        }
    }
}
