using System;
using UnityEditor;
using UnityEngine;

namespace SweepNDodge.DotsBullets.Editor
{
    internal abstract class WaveManagedReferenceDrawerBase : PropertyDrawer
    {
        private const float VerticalSpacing = 2f;
        private const float HelpBoxHeight = 34f;

        protected abstract Type[] ConcreteTypes { get; }
        protected virtual bool ShouldDrawChildProperty(SerializedProperty parentProperty, SerializedProperty childProperty) => true;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float height = EditorGUIUtility.singleLineHeight;
            if (property.managedReferenceValue == null)
                return height + VerticalSpacing + HelpBoxHeight;

            var iterator = property.Copy();
            var end = iterator.GetEndProperty();
            bool enterChildren = true;
            while (iterator.NextVisible(enterChildren) && !SerializedProperty.EqualContents(iterator, end))
            {
                if (!ShouldDrawChildProperty(property, iterator))
                {
                    enterChildren = false;
                    continue;
                }

                height += VerticalSpacing + EditorGUI.GetPropertyHeight(iterator, true);
                enterChildren = false;
            }

            return height;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            Rect headerRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            Rect contentRect = EditorGUI.PrefixLabel(headerRect, label);
            if (EditorGUI.DropdownButton(contentRect, new GUIContent(GetCurrentTypeLabel(property)), FocusType.Keyboard))
                ShowTypeMenu(property);

            if (property.managedReferenceValue == null)
            {
                Rect helpRect = new Rect(position.x, headerRect.yMax + VerticalSpacing, position.width, HelpBoxHeight);
                EditorGUI.HelpBox(helpRect, "Select a concrete type for this SerializeReference field.", MessageType.Info);
                EditorGUI.EndProperty();
                return;
            }

            float y = headerRect.yMax + VerticalSpacing;
            using (new EditorGUI.IndentLevelScope())
            {
                var iterator = property.Copy();
                var end = iterator.GetEndProperty();
                bool enterChildren = true;
                while (iterator.NextVisible(enterChildren) && !SerializedProperty.EqualContents(iterator, end))
                {
                    if (!ShouldDrawChildProperty(property, iterator))
                    {
                        enterChildren = false;
                        continue;
                    }

                    float childHeight = EditorGUI.GetPropertyHeight(iterator, true);
                    var childRect = new Rect(position.x, y, position.width, childHeight);
                    EditorGUI.PropertyField(childRect, iterator, true);
                    y += childHeight + VerticalSpacing;
                    enterChildren = false;
                }
            }

            EditorGUI.EndProperty();
        }

        private void ShowTypeMenu(SerializedProperty property)
        {
            string propertyPath = property.propertyPath;
            var serializedObject = property.serializedObject;
            Type currentType = property.managedReferenceValue?.GetType();
            var menu = new GenericMenu();

            for (int i = 0; i < ConcreteTypes.Length; i++)
            {
                Type type = ConcreteTypes[i];
                bool isCurrent = type == currentType;
                menu.AddItem(
                    new GUIContent(GetTypeLabel(type)),
                    isCurrent,
                    () => AssignManagedReference(serializedObject, propertyPath, type));
            }

            menu.AddSeparator(string.Empty);
            menu.AddItem(
                new GUIContent("None (Invalid)"),
                property.managedReferenceValue == null,
                () => AssignManagedReference(serializedObject, propertyPath, null));

            menu.ShowAsContext();
        }

        private static void AssignManagedReference(SerializedObject serializedObject, string propertyPath, Type type)
        {
            serializedObject.Update();
            var targetProperty = serializedObject.FindProperty(propertyPath);
            if (targetProperty == null)
                return;

            targetProperty.managedReferenceValue = type != null ? Activator.CreateInstance(type) : null;
            serializedObject.ApplyModifiedProperties();
            if (serializedObject.targetObject != null)
                EditorUtility.SetDirty(serializedObject.targetObject);
        }

        private static string GetCurrentTypeLabel(SerializedProperty property)
        {
            return property.managedReferenceValue == null
                ? "<Select Type>"
                : GetTypeLabel(property.managedReferenceValue.GetType());
        }

        private static string GetTypeLabel(Type type)
        {
            string name = type.Name;
            name = name.Replace("EmissionAuthoring", string.Empty);
            name = name.Replace("SamplingAnchorAuthoring", string.Empty);
            name = name.Replace("AreaSamplerAuthoring", string.Empty);
            name = name.Replace("PositionPatternAuthoring", string.Empty);
            name = name.Replace("ShotPatternAuthoring", string.Empty);
            name = name.Replace("AimAuthoring", string.Empty);
            name = name.Replace("Authoring", string.Empty);
            return ObjectNames.NicifyVariableName(name);
        }
    }

    [CustomPropertyDrawer(typeof(WaveEmissionAuthoringBase), true)]
    internal sealed class WaveEmissionAuthoringDrawer : WaveManagedReferenceDrawerBase
    {
        private static readonly Type[] Types =
        {
            typeof(RateFieldEmissionAuthoring),
            typeof(PoissonEmissionAuthoring),
            typeof(EventBurstEmissionAuthoring),
        };

        protected override Type[] ConcreteTypes => Types;

        protected override bool ShouldDrawChildProperty(SerializedProperty parentProperty, SerializedProperty childProperty)
        {
            if (childProperty.name != nameof(PoissonEmissionAuthoring.EventShotIntervalSec))
                return true;

            var scheduleProperty = parentProperty.FindPropertyRelative(nameof(PoissonEmissionAuthoring.EventShotSchedule));
            return scheduleProperty != null
                && scheduleProperty.propertyType == SerializedPropertyType.Enum
                && scheduleProperty.enumValueIndex == (int)SourceSpawnEventShotScheduleId.Timed;
        }
    }

    [CustomPropertyDrawer(typeof(WaveSamplingAnchorAuthoringBase), true)]
    internal sealed class WaveSamplingAnchorAuthoringDrawer : WaveManagedReferenceDrawerBase
    {
        private static readonly Type[] Types =
        {
            typeof(SourceCenterSamplingAnchorAuthoring),
            typeof(FixedPointSamplingAnchorAuthoring),
            typeof(PlayerRelativeSamplingAnchorAuthoring),
        };

        protected override Type[] ConcreteTypes => Types;
    }

    [CustomPropertyDrawer(typeof(WaveAreaSamplerAuthoringBase), true)]
    internal sealed class WaveAreaSamplerAuthoringDrawer : WaveManagedReferenceDrawerBase
    {
        private static readonly Type[] Types =
        {
            typeof(CenterPointAreaSamplerAuthoring),
            typeof(UniformFieldAreaSamplerAuthoring),
            typeof(PollutionTopKAreaSamplerAuthoring),
        };

        protected override Type[] ConcreteTypes => Types;
    }

    [CustomPropertyDrawer(typeof(WavePositionPatternAuthoringBase), true)]
    internal sealed class WavePositionPatternAuthoringDrawer : WaveManagedReferenceDrawerBase
    {
        private static readonly Type[] Types =
        {
            typeof(SinglePointPositionPatternAuthoring),
            typeof(LineEvenPositionPatternAuthoring),
            typeof(PointSetPositionPatternAuthoring),
        };

        protected override Type[] ConcreteTypes => Types;
    }

    [CustomPropertyDrawer(typeof(WaveAimAuthoringBase), true)]
    internal sealed class WaveAimAuthoringDrawer : WaveManagedReferenceDrawerBase
    {
        private static readonly Type[] Types =
        {
            typeof(RandomAimAuthoring),
            typeof(FixedAimAuthoring),
            typeof(LineNormalAimAuthoring),
            typeof(SpiralAimAuthoring),
            typeof(PlayerPositionAimAuthoring),
        };

        protected override Type[] ConcreteTypes => Types;
    }

    [CustomPropertyDrawer(typeof(WaveShotPatternAuthoringBase), true)]
    internal sealed class WaveShotPatternAuthoringDrawer : WaveManagedReferenceDrawerBase
    {
        private static readonly Type[] Types =
        {
            typeof(SingleShotPatternAuthoring),
            typeof(NWayShotPatternAuthoring),
            typeof(RadialShotPatternAuthoring),
        };

        protected override Type[] ConcreteTypes => Types;
    }
}
