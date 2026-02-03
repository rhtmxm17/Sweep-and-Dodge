#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class DotsGuidLookup
{
    [MenuItem("Tools/DOTS/Lookup EntityScene GUID")]
    static void Lookup()
    {
        var guid = "d1dfdd2e0e6c46a469a0e59d85d2c155";
        Debug.Log(AssetDatabase.GUIDToAssetPath(guid));
    }
}
#endif
