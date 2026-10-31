using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

// One GameObject from the original game's prefab: name, local transform and children. Nothing else
// is kept — the importer only needs where things are, not their components.
public class OriginalPrefabNode
{
    public string Name;
    public Vector3 LocalPosition;
    public Quaternion LocalRotation = Quaternion.identity;
    public Vector3 LocalScale = Vector3.one;
    public OriginalPrefabNode Parent;
    public readonly List<OriginalPrefabNode> Children = new List<OriginalPrefabNode>();

    // Name without Unity's duplicate suffix: "PickUp_LongRail (7)" -> "PickUp_LongRail".
    public string BaseName => Regex.Replace(Name, @"\s*\(\d+\)$", "");

    public Matrix4x4 LocalToWorld =>
        (Parent != null ? Parent.LocalToWorld : Matrix4x4.identity) * Matrix4x4.TRS(LocalPosition, LocalRotation, LocalScale);

    public Vector3 WorldPosition => LocalToWorld.GetPosition();

    // Uniform world scale; measured on a direction because lossyScale is unreliable under a mirror.
    public float WorldScale => LocalToWorld.MultiplyVector(Vector3.right).magnitude;

    // Heading around Y only — every piece in the original lies flat.
    public float WorldYaw
    {
        get
        {
            Vector3 forward = LocalToWorld.MultiplyVector(Vector3.forward);
            return Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
        }
    }

    // Case-insensitive: the original isn't consistent ("Pickups" in most sections, "PickUps" in some).
    public OriginalPrefabNode FindChild(string name) =>
        Children.Find(c => string.Equals(c.Name, name, System.StringComparison.OrdinalIgnoreCase));
}

// Reads a prefab exported by AssetRipper (Unity text YAML) into a tree of OriginalPrefabNodes.
// Only GameObjects (class 1) and Transforms (class 4) are looked at. Editor-only, read-only.
public static class OriginalPrefabReader
{
    private static readonly Regex DocumentHeader = new Regex(@"^--- !u!(\d+) &(-?\d+)", RegexOptions.Multiline);

    /// <summary>mirrored = the whole prefab flipped left-right (X axis): every position, heading and island centre
    /// comes out mirrored. Done with an invisible parent that has scale (-1, 1, 1), so nothing else has to care.
    /// What does have to care is a piece's own bend direction (see OriginalTrackBuilder.Piece.mirrored).</summary>
    public static OriginalPrefabNode Read(string path, bool mirrored = false)
    {
        OriginalPrefabNode root = ReadTree(path);
        if (mirrored && root != null) root.Parent = new OriginalPrefabNode { Name = "Mirror", LocalScale = new Vector3(-1f, 1f, 1f) };
        return root;
    }

    private static OriginalPrefabNode ReadTree(string path)
    {
        string text = File.ReadAllText(path);
        var names = new Dictionary<long, string>();          // GameObject id -> name
        var transforms = new Dictionary<long, string>();     // Transform id -> its YAML body

        MatchCollection headers = DocumentHeader.Matches(text);
        for (int i = 0; i < headers.Count; i++)
        {
            int start = headers[i].Index + headers[i].Length;
            int end = i + 1 < headers.Count ? headers[i + 1].Index : text.Length;
            string body = text.Substring(start, end - start);
            int classId = int.Parse(headers[i].Groups[1].Value);
            long id = long.Parse(headers[i].Groups[2].Value);

            if (classId == 1) names[id] = Field(body, "m_Name");
            else if (classId == 4) transforms[id] = body;
        }

        var nodes = new Dictionary<long, OriginalPrefabNode>();
        foreach (KeyValuePair<long, string> t in transforms)
        {
            nodes[t.Key] = new OriginalPrefabNode
            {
                Name = names.TryGetValue(FileId(Field(t.Value, "m_GameObject")), out string n) ? n : "?",
                LocalPosition = ReadVector(Field(t.Value, "m_LocalPosition")),
                LocalRotation = ReadQuaternion(Field(t.Value, "m_LocalRotation")),
                LocalScale = ReadVector(Field(t.Value, "m_LocalScale")),
            };
        }

        OriginalPrefabNode root = null;
        foreach (KeyValuePair<long, string> t in transforms)
        {
            OriginalPrefabNode node = nodes[t.Key];
            if (nodes.TryGetValue(FileId(Field(t.Value, "m_Father")), out OriginalPrefabNode parent))
            {
                node.Parent = parent;
                parent.Children.Add(node);
            }
            else root = node;
        }
        return root;
    }

    private static string Field(string body, string key)
    {
        Match m = Regex.Match(body, @"\n\s*" + key + @": ([^\n]*)");
        return m.Success ? m.Groups[1].Value.Trim() : "";
    }

    private static long FileId(string value)
    {
        Match m = Regex.Match(value, @"fileID: (-?\d+)");
        return m.Success ? long.Parse(m.Groups[1].Value) : 0;
    }

    private static float Component(string value, string axis)
    {
        Match m = Regex.Match(value, axis + @": (-?[\d.eE+-]+)");
        return m.Success ? float.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) : 0f;
    }

    private static Vector3 ReadVector(string v) => new Vector3(Component(v, "x"), Component(v, "y"), Component(v, "z"));

    private static Quaternion ReadQuaternion(string v) =>
        new Quaternion(Component(v, "x"), Component(v, "y"), Component(v, "z"), Component(v, "w"));
}
