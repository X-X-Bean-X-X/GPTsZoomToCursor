using System;
using System.Collections.Generic;
using System.Reflection;
using Game.Input;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GPTZoomToCursor
{
    // CS2's SmoothProcessor changes its history when ReadValue is called. Peek without
    // advancing that history, so the native camera still sees its original input/speed.
    internal sealed class NativeInputPeek
    {
        private sealed class History
        {
            internal object Processor;
            internal FieldInfo[] Fields;
            internal readonly object[] Values = new object[4];
        }
        private static readonly FieldInfo StateField = typeof(InputActionMap).GetField("m_State", BindingFlags.Instance | BindingFlags.NonPublic);
        private object cachedState;
        private object cachedArray;
        private readonly List<History> histories = new List<History>();

        internal float ReadZoom(InputAction source, ProxyAction zoom, ProxyAction move, ProxyAction fast, ProxyAction rotate, out bool moving)
        {
            object state = StateField?.GetValue(source.actionMap);
            if (state == null) throw new InvalidOperationException("Native input state unavailable");
            var processors = (InputProcessor[])state.GetType().GetField("processors").GetValue(state);
            if (!ReferenceEquals(state, cachedState) || !ReferenceEquals(processors, cachedArray))
            {
                histories.Clear();
                foreach (var processor in processors)
                {
                    Type type = processor is SmoothFloatProcessor ? typeof(SmoothProcessor<float>)
                        : processor is SmoothVector2Processor ? typeof(SmoothProcessor<Vector2>) : null;
                    if (type == null) continue;
                    var entry = new History { Processor = processor, Fields = new FieldInfo[4] };
                    string[] names = { "m_LastValue", "m_LastFrame", "m_LastTime", "m_LastDelta" };
                    for (int i = 0; i < names.Length; i++)
                        entry.Fields[i] = type.GetField(names[i], BindingFlags.Instance | BindingFlags.NonPublic)
                            ?? throw new MissingFieldException(type.FullName, names[i]);
                    histories.Add(entry);
                }
                cachedState = state;
                cachedArray = processors;
            }
            foreach (var entry in histories)
                for (int i = 0; i < 4; i++) entry.Values[i] = entry.Fields[i].GetValue(entry.Processor);
            try
            {
                moving = move.ReadValue<Vector2>().sqrMagnitude > 1E-07f
                    || fast.ReadValue<Vector2>().sqrMagnitude > 1E-07f
                    || rotate.ReadValue<Vector2>().sqrMagnitude > 1E-07f;
                return zoom.ReadValue<float>();
            }
            finally
            {
                foreach (var entry in histories)
                    for (int i = 0; i < 4; i++) entry.Fields[i].SetValue(entry.Processor, entry.Values[i]);
            }
        }
    }
}
