// <copyright file="IJobEvent.cs" company="BovineLabs">
//     Copyright (c) BovineLabs. All rights reserved.
// </copyright>

namespace BovineLabs.Event.Utility
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using BovineLabs.Event.Systems;
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Jobs;
    using Unity.Jobs.LowLevel.Unsafe;

    [SuppressMessage("ReSharper", "TypeParameterCanBeVariant", Justification = "Strict requirements for compiler")]
    public interface IJobEvent<T>
        where T : struct
    {
        void Execute(T value);
    }

    public static class JobEvent
    {
        public static unsafe JobHandle ScheduleSequential<TJob, T>(
            this TJob jobData, EventSystem eventSystem, int minIndicesPerJobCount, JobHandle dependsOn = default)
            where TJob : struct, IJobEvent<T>
            where T : struct
        {
            dependsOn = eventSystem.GetEventReaders<T>(dependsOn, out var events);

            for (var i = 0; i < events.Length; i++)
            {
                var e = events[i];

                var fullData = new EventJobStruct<TJob, T>
                {
                    Reader = e.Item1,
                    JobData = jobData,
                };

                var scheduleParams = new JobsUtility.JobScheduleParameters(
                    UnsafeUtility.AddressOf(ref fullData),
                    EventJobStruct<TJob, T>.Initialize(),
                    dependsOn,
                    ScheduleMode.Batched);

                dependsOn = JobsUtility.ScheduleParallelFor(
                    ref scheduleParams,
                    e.Item2,
                    minIndicesPerJobCount);
            }

            eventSystem.AddJobHandleForConsumer<T>(dependsOn);

            return dependsOn;
        }

        internal struct EventJobStruct<TJob, T>
            where TJob : struct, IJobEvent<T>
            where T : struct
        {
            [ReadOnly]
            public NativeStream.Reader Reader;

            public TJob JobData;

            // ReSharper disable once StaticMemberInGenericType
            private static IntPtr jobReflectionData;

            private delegate void ExecuteJobFunction(
                ref EventJobStruct<TJob, T> fullData,
                IntPtr additionalPtr,
                IntPtr bufferRangePatchData,
                ref JobRanges ranges,
                int jobIndex);

            internal static IntPtr Initialize()
            {
                if (jobReflectionData == IntPtr.Zero)
                {
                    jobReflectionData = JobsUtility.CreateJobReflectionData(
                        typeof(EventJobStruct<TJob, T>),
                        typeof(TJob),
                        JobType.ParallelFor,
                        (ExecuteJobFunction)Execute);
                }

                return jobReflectionData;
            }

            // ReSharper disable once MemberCanBePrivate.Global - Required by Burst
            public static void Execute(
                ref EventJobStruct<TJob, T> fullData,
                IntPtr additionalPtr,
                IntPtr bufferRangePatchData,
                ref JobRanges ranges,
                int jobIndex)
            {
                while (true)
                {
                    if (!JobsUtility.GetWorkStealingRange(ref ranges, jobIndex, out var begin, out var end))
                    {
                        return;
                    }

                    for (int i = begin; i < end; i++)
                    {
                        var count = fullData.Reader.BeginForEachIndex(begin);

                        for (var j = 0; j < count; j++)
                        {
                            var e = fullData.Reader.Read<T>();
                            fullData.JobData.Execute(e);
                        }

                        fullData.Reader.EndForEachIndex();
                    }
                }
            }
        }
    }
}