using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;

// Shared Inspector fields and Scene-view handles for anything placed by a TrackPlacement
// (TrackPlacedStack, TrackPlacedStamp). The target must implement ITrackPlaced and keep its
// placement in a serialized field named "placement".
public abstract class TrackPlacedEditor : Editor
{
    private SerializedProperty road, sectionId, distance, side, facing, yaw, height;

    protected virtual void OnEnable()
    {
        SerializedProperty placement = serializedObject.FindProperty("placement");
        road = placement.FindPropertyRelative(nameof(TrackPlacement.road));
        sectionId = placement.FindPropertyRelative(nameof(TrackPlacement.sectionId));
        distance = placement.FindPropertyRelative(nameof(TrackPlacement.distance));
        side = placement.FindPropertyRelative(nameof(TrackPlacement.side));
        facing = placement.FindPropertyRelative(nameof(TrackPlacement.facing));
        yaw = placement.FindPropertyRelative(nameof(TrackPlacement.yaw));
        height = placement.FindPropertyRelative(nameof(TrackPlacement.height));

        // The Transform is driven by the placement — Unity's Move tool would just get overwritten.
        // Our own track handles replace it while a placed object is selected.
        // On a platform the Transform is free, so there Unity's own Move/Rotate tools stay.
        Tools.hidden = OnPlatformCount() == 0;
    }

    protected int OnPlatformCount()
    {
        int count = 0;
        foreach (Object t in targets)
            if (t is ITrackPlaced placed && placed.Platform != null) count++;
        return count;
    }

    /// <summary>
    /// For objects on a platform: shows which one and a "Back To Road" button, instead of the road fields.
    /// Returns false when nothing selected is on a platform — then draw the road fields as usual.
    /// </summary>
    protected bool DrawPlatformSection()
    {
        int onPlatform = OnPlatformCount();
        if (onPlatform == 0) return false;

        if (onPlatform < targets.Length)
        {
            EditorGUILayout.HelpBox("Some selected objects are on a platform and some on the road — " +
                                    "select only one kind to edit them.", MessageType.Info);
            return true;
        }

        string platformName = targets.Length == 1 ? ((ITrackPlaced)target).Platform.name : "a platform";
        EditorGUILayout.HelpBox($"On {platformName}. Move and rotate it with the normal Move / Rotate tools — " +
                                "it moves with the platform.", MessageType.Info);

        if (GUILayout.Button(new GUIContent("Back To Road",
                "Takes it off the platform and puts it on the nearest point of the road, centred.")))
        {
            foreach (Object t in targets)
                ((ITrackPlaced)t).BackToRoad("Back To Road");
            Tools.hidden = true; // back on the road: the track handles take over again
        }
        return true;
    }

    protected virtual void OnDisable()
    {
        Tools.hidden = false;
    }

    // Call between serializedObject.Update() and ApplyModifiedProperties().
    protected void DrawPlacementFields()
    {
        EditorGUILayout.PropertyField(road);
        var roadObject = road.objectReferenceValue as SplineRoad;

        if (roadObject != null && !road.hasMultipleDifferentValues)
        {
            DrawSectionPopup(roadObject);
            DrawDistanceSlider(roadObject);
        }
        else
        {
            EditorGUILayout.PropertyField(sectionId);
            EditorGUILayout.PropertyField(distance);
        }

        EditorGUILayout.PropertyField(side);
        EditorGUILayout.PropertyField(facing);
        EditorGUILayout.PropertyField(yaw, new GUIContent("Extra Yaw", yaw.tooltip));
        EditorGUILayout.PropertyField(height);
    }

    // Call after ApplyModifiedProperties(), so it reflects the values just edited.
    protected void DrawStatusAndSnap()
    {
        if (targets.Length == 1 && !TrackMath.TryEvaluate(((ITrackPlaced)target).Placement, out _, out _))
            EditorGUILayout.HelpBox("Not on a road yet. Press \"Snap To Road Centre\" to find the road " +
                                    "and place it where the object is now.", MessageType.Warning);

        EditorGUILayout.Space();
        if (GUILayout.Button(new GUIContent("Snap To Road Centre",
                "Finds the road if the Road field is empty (always the case for a prefab just dragged in), " +
                "then places this at the nearest point along the track, in the middle of the road (Side 0).")))
        {
            foreach (Object t in targets)
                SnapToRoadCentre((ITrackPlaced)t);
        }
    }

    private static void SnapToRoadCentre(ITrackPlaced placed)
    {
        TrackPlacement placement = placed.Placement;

        // A prefab can't reference a road inside a level, so a freshly dropped one arrives with none.
        if (placement.road == null) placement.road = TrackMath.FindRoad(placed.transform.gameObject);
        if (placement.road == null)
        {
            Debug.LogWarning($"{placed.transform.name}: no SplineRoad found in this level. " +
                             "Add one, or drag it into the Road field.", placed.transform);
            return;
        }

        placement.road.EnsureSectionIds();
        if (!TrackMath.TryProject(placement.road, placed.transform.position, ref placement)) return;

        placement.side = 0f;
        placed.SetPlacement(placement, "Snap To Road Centre");
    }

    // Sections are shown by their current index, but the ID is what gets saved.
    private void DrawSectionPopup(SplineRoad roadObject)
    {
        roadObject.EnsureSectionIds();
        int count = roadObject.SectionCount;
        int current = sectionId.hasMultipleDifferentValues ? -1 : roadObject.FindSectionIndex(sectionId.intValue);

        var labels = new string[count];
        for (int i = 0; i < count; i++)
            labels[i] = $"Spline {i}  (width {roadObject.GetWidth(i):0.#}, " +
                        $"length {roadObject.Container.Splines[i].GetLength():0.#})";

        EditorGUI.showMixedValue = sectionId.hasMultipleDifferentValues;
        EditorGUI.BeginChangeCheck();
        int picked = EditorGUILayout.Popup("Section", current, labels);
        if (EditorGUI.EndChangeCheck() && picked >= 0)
            sectionId.intValue = roadObject.GetSectionId(picked);
        EditorGUI.showMixedValue = false;

        if (current < 0 && !sectionId.hasMultipleDifferentValues)
            EditorGUILayout.HelpBox("Section not found (its spline may have been deleted). Pick one.", MessageType.Warning);
    }

    // Slider limited to the chosen section's real length, so it can't point past the end.
    private void DrawDistanceSlider(SplineRoad roadObject)
    {
        int index = sectionId.hasMultipleDifferentValues ? -1 : roadObject.FindSectionIndex(sectionId.intValue);
        if (index < 0)
        {
            EditorGUILayout.PropertyField(distance);
            return;
        }

        float length = roadObject.Container.Splines[index].GetLength();
        EditorGUI.showMixedValue = distance.hasMultipleDifferentValues;
        EditorGUI.BeginChangeCheck();
        float value = EditorGUILayout.Slider(new GUIContent("Distance", distance.tooltip), distance.floatValue, 0f, length);
        if (EditorGUI.EndChangeCheck()) distance.floatValue = value;
        EditorGUI.showMixedValue = false;
    }

    // Arrows move along / across the road, the square moves freely on the road plane.
    // Wherever the drag ends, the point is projected back onto the track and saved as a placement.
    // Protected, not private: Unity looks this message up on the derived editor class.
    protected virtual void OnSceneGUI()
    {
        var placed = (ITrackPlaced)target;
        TrackPlacement placement = placed.Placement;
        if (placement.road == null || placed.Platform != null) return; // track handles are for the road only

        Transform t = placed.transform;
        Vector3 position = t.position;
        float size = HandleUtility.GetHandleSize(position);

        EditorGUI.BeginChangeCheck();

        Handles.color = Handles.zAxisColor;
        Vector3 moved = Handles.Slider(position, t.forward, size, Handles.ArrowHandleCap, 0f);

        Handles.color = Handles.xAxisColor;
        Vector3 across = Handles.Slider(position, t.right, size, Handles.ArrowHandleCap, 0f);
        if (across != position) moved = across;

        Handles.color = Handles.yAxisColor;
        Vector3 free = Handles.Slider2D(position, t.up, t.forward, t.right, size * 0.15f,
            Handles.RectangleHandleCap, Vector2.zero);
        if (free != position) moved = free;

        if (EditorGUI.EndChangeCheck() && TrackMath.TryProject(placement.road, moved, ref placement))
        {
            TrackPlacementSettings.Snap(ref placement); // same Distance Step / Lanes as the placement window
            placed.SetPlacement(placement, "Move On Track");
        }
    }
}
