#if GODOT
using Godot;
#endif
using System;
using System.Runtime.CompilerServices;
using System.Collections.Concurrent;

namespace PhaseEngine
{
    public static class Glue
    {
        /// [Stand-in for] Property indexer.  Used to talk to/from Godot for convenience.  
        /// In GDScript you can use obj.set(prop, val) or obj.get(prop); this is a similar feature for c#.
        /// Any class which implements the interface can use these methods to adjust a field or property of itself (provided it's public..)
        //TODO:  Make this safer
        public static Type GetValType<T>(this T instance, string propertyName)  
        {
                Type type = typeof(T);
                var property = type.GetProperty(propertyName);
                if (property==null)
                   return type.GetField(propertyName)?.FieldType;

                return property.PropertyType;
;
        } 
        public static object GetVal<T>(this T instance, string propertyName)  
        {
                Type type = typeof(T);
                System.Reflection.PropertyInfo property = type.GetProperty(propertyName);

                if (property==null)
                {
                    System.Reflection.FieldInfo field = type.GetField(propertyName);
                    return field.GetValue(instance);
                }

                return property.GetValue(instance);
        } 
       public static void SetVal<T>(this T instance, string propertyName, object value)  where T:class
        {
            Type type = typeof(T);
            System.Reflection.PropertyInfo property = type.GetProperty(propertyName);

            if(property==null)
            {
                System.Reflection.FieldInfo field = type.GetField(propertyName);

                //Try to force unchecked conversion to the target type
                var unboxedVal2 = Convert.ChangeType(value, field.FieldType);

                field.SetValue(instance, unboxedVal2);

                return;
            }
            //Try to force unchecked conversion to the target type
            var unboxedVal = Convert.ChangeType(value, property.PropertyType);
            property.SetValue(instance, unboxedVal);
        }
       public static void SetVal<T>(ref this T instance, string propertyName, object value) where T:struct
        {
            Type type = typeof(T);
            System.Reflection.PropertyInfo property = type.GetProperty(propertyName);

            if(property==null)
            {
                System.Reflection.FieldInfo field = type.GetField(propertyName);
                var self = __makeref(instance);

                //Try to force unchecked conversion to the target type
                var unboxedVal2 = Convert.ChangeType(value, field.FieldType);

                //Pretty evil thing to do here, but it's the only way to make sure this works on a struct
                field.SetValueDirect(self, unboxedVal2);
                return;
            }
            //Try to force unchecked conversion to the target type
            var unboxedVal = Convert.ChangeType(value, property.PropertyType);
            object box = RuntimeHelpers.GetObjectValue(instance);
            property.SetValue(box, value);
            instance = (T)box;  //Copy back the box over ourself
        }
        

    }

    public class ObjectPool<T>
    {
        readonly ConcurrentBag<T> _objects;
        readonly Func<T> _objectGenerator;

        // //Generates a warning.......
        // public static ObjectPool<T> Prototype<T>() where T: new()  //Default ctor convenience func
        // { return new ObjectPool<T>( () => new T() ); }

       public ObjectPool(Func<T> objectGenerator)
        {
            _objectGenerator = objectGenerator ?? throw new ArgumentNullException(nameof(objectGenerator));
            _objects = new ConcurrentBag<T>();
        }

        public void Clear() => _objects.Clear();
        public T Get() => _objects.TryTake(out T item) ? item : _objectGenerator();
        public void Return(T item) => _objects.Add(item);


    }


}