// Wrapper for the Attribute added in the latest collections package
// This maintains support for older versions
#if !UNITY_COLLECTIONS_0_14_OR_NEWER
// ReSharper disable once CheckNamespace
namespace Unity.Collections
{
    using System;
    using Unity.Jobs;

    /// <summary>
    /// Documents and enforces (via generated tests) that the tagged method or property has to stay burst compatible.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Method | AttributeTargets.Property, AllowMultiple = true)]
    public class BurstCompatibleAttribute : Attribute
    {
        public Type[] GenericTypeArguments { get; set; }

        public string RequiredUnityDefine = null;
    }

    /// <summary>
    /// Internal attribute to state that a method is not burst compatible even though the containing type is.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Property)]
    public class NotBurstCompatibleAttribute : Attribute
    {
    }

    public interface INativeDisposable : IDisposable
    {
        JobHandle Dispose(JobHandle inputDeps);
    }
}
#endif