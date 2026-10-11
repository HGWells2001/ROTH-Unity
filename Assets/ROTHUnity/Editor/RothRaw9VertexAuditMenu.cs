#if UNITY_EDITOR
using System.Text;
using ROTHUnity.Core;
using UnityEditor;
using UnityEngine;

namespace ROTHUnity.Editor
{
    public static class RothRaw9VertexAuditMenu
    {
        [MenuItem("ROTH/Audit original RAW9 vertex references...")]
        private static void Audit()
        {
            string path = EditorUtility.OpenFilePanel("Select an original ROTH RAW map", "", "raw");
            if (string.IsNullOrEmpty(path)) return;
            try
            {
                RothRawMap map = RothRawMapReader.Read(path);
                var report = new StringBuilder();
                int checkedCount = 0, rejected = 0, extraFaces = 0;
                foreach (RothCommand c in map.Commands)
                {
                    if (c.BaseOpcode != 9 || c.Arguments == null || c.Arguments.Length < 6) continue;
                    ushort sectorId = c.Arguments[1];
                    bool alongX = (c.Arguments[0] & 0x40) != 0;
                    checkedCount++;
                    RothRaw9VertexInspector.Selection selected;
                    string reason;
                    if (!RothRaw9VertexInspector.TrySelect(map, sectorId, alongX, out selected, out reason))
                    {
                        rejected++;
                        report.AppendLine("Sector " + sectorId + ": CHECK REQUIRED: " + reason);
                        continue;
                    }
                    if (selected.FacesCount != 4) extraFaces++;
                    report.Append("Sector ").Append(sectorId).Append(alongX ? " X: " : " Y: ");
                    for (int i = 0; i < 4; i++)
                        report.Append("v").Append(selected.Indices[i]).Append('(')
                            .Append(selected.Coordinates[i]).Append(") ");
                    if (selected.FacesCount != 4) report.Append("[EXTRA FACES: ").Append(selected.FacesCount).Append("]");
                    report.AppendLine();
                }
                Debug.Log("RAW9 reference audit: " + path + " | " + checkedCount +
                          " commands, " + rejected + " rejected, " + extraFaces + " have extra faces.\n" + report);
                EditorUtility.DisplayDialog("ROTH RAW9 vertex audit",
                    checkedCount + " commands checked; " + rejected + " rejected; " + extraFaces + " non-quads.\nDetails in the Console.", "OK");
            }
            catch (System.Exception ex)
            {
                Debug.LogException(ex);
                EditorUtility.DisplayDialog("ROTH RAW9 audit failed", ex.Message, "OK");
            }
        }
    }
}
#endif
