using System;
using System.Collections.Generic;
using System.Reflection;
using System.Xml;

namespace Builder.Data;

public static class ElementExtensions
{
	private static readonly MethodInfo CloneMethod = typeof(object).GetMethod("MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic);

	public static bool IsPrimitive(this Type type)
	{
		if (type == typeof(string))
		{
			return true;
		}
		return type.IsValueType & type.IsPrimitive;
	}

	public static object Copy(this object originalObject)
	{
		return InternalCopy(originalObject, new Dictionary<object, object>(new ReferenceEqualityComparer()), false);
	}

	/// <summary>
	/// Copies a graph while keeping XML nodes as independent subtrees. Source snapshots need
	/// their own element XML, not a reflective copy of its parent document and every sibling.
	/// Ordinary <see cref="Copy{T}(T)"/> retains its existing graph-copy behavior.
	/// </summary>
	public static T CopyWithDetachedXml<T>(this T original)
	{
		return (T)InternalCopy(original, new Dictionary<object, object>(new ReferenceEqualityComparer()), true);
	}

	private static object InternalCopy(object originalObject, IDictionary<object, object> visited, bool detachXml)
	{
		if (originalObject == null)
		{
			return null;
		}
		Type type = originalObject.GetType();
		if (type.IsPrimitive())
		{
			return originalObject;
		}
		if (visited.ContainsKey(originalObject))
		{
			return visited[originalObject];
		}
		if (detachXml && originalObject is XmlNode node)
		{
			XmlNode copiedNode = node is XmlDocument
				? node.CloneNode(true)
				: new XmlDocument().ImportNode(node, true);
			visited.Add(originalObject, copiedNode);
			return copiedNode;
		}
		if (typeof(Delegate).IsAssignableFrom(type))
		{
			return null;
		}
		object obj = CloneMethod.Invoke(originalObject, null);
		if (type.IsArray && !type.GetElementType().IsPrimitive())
		{
			Array clonedArray = (Array)obj;
			clonedArray.ForEach(delegate(Array array, int[] indices)
			{
				array.SetValue(InternalCopy(clonedArray.GetValue(indices), visited, detachXml), indices);
			});
		}
		visited.Add(originalObject, obj);
		CopyFields(originalObject, visited, obj, type, detachXml);
		RecursiveCopyBaseTypePrivateFields(originalObject, visited, obj, type, detachXml);
		return obj;
	}

	private static void RecursiveCopyBaseTypePrivateFields(object originalObject, IDictionary<object, object> visited, object cloneObject, Type typeToReflect, bool detachXml)
	{
		if (typeToReflect.BaseType != null)
		{
			RecursiveCopyBaseTypePrivateFields(originalObject, visited, cloneObject, typeToReflect.BaseType, detachXml);
			CopyFields(originalObject, visited, cloneObject, typeToReflect.BaseType, detachXml, BindingFlags.Instance | BindingFlags.NonPublic, (FieldInfo info) => info.IsPrivate);
		}
	}

	private static void CopyFields(object originalObject, IDictionary<object, object> visited, object cloneObject, Type typeToReflect, bool detachXml, BindingFlags bindingFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy, Func<FieldInfo, bool> filter = null)
	{
		FieldInfo[] fields = typeToReflect.GetFields(bindingFlags);
		foreach (FieldInfo fieldInfo in fields)
		{
			if ((filter == null || filter(fieldInfo)) && !fieldInfo.FieldType.IsPrimitive())
			{
				object value = InternalCopy(fieldInfo.GetValue(originalObject), visited, detachXml);
				fieldInfo.SetValue(cloneObject, value);
			}
		}
	}

	public static T Copy<T>(this T original)
	{
		return (T)((object)original).Copy();
	}
}
