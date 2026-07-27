using System;
using SolidWorks.Interop.sldworks;

namespace SolidworksDiscordRPC
{
    /// <summary>
    /// Reads optional document metadata (material, dirty state, feature count).
    /// Every read is individually try/catch-guarded — none of these are essential.
    /// </summary>
    internal struct EnrichedDocInfo
    {
        public int DocType;
        public string Title;
        public string PathName;
        public bool IsDirty;
        public bool NeedsRebuild;
        public string MaterialName;
        public int FeatureCount;
        public bool HasFeatureCount;
        public bool HasMaterial;
    }

    internal static class DocumentInfoProvider
    {
        public static EnrichedDocInfo ReadEnriched(ModelDoc2 doc)
        {
            var info = new EnrichedDocInfo
            {
                DocType = SafeDocType(doc),
                Title = SafeTitle(doc),
                PathName = SafePathName(doc)
            };

            // Dirty flag — IModelDoc2.GetSaveFlag() exists on all doc types
            try { info.IsDirty = doc.GetSaveFlag(); } catch { /* ignore */ }

            // NeedsRebuild is on IModelDocExtension.get_NeedsRebuild() / NeedsRebuild2
            // per real interop DLL (D:\SOLIDWORKS Corp\SOLIDWORKS\api\redist)
            try
            {
                var ext = doc.Extension;
                if (ext != null)
                {
                    // Property accessor exposed as NeedsRebuild on the IDispatch interface,
                    // backing property is NeedsRebuild/NeedsRebuild2
                    info.NeedsRebuild = ext.NeedsRebuild;
                }
            }
            catch { /* ignore */ }

            // Feature count — meaningful for Part/Assembly; best-effort for Drawing too
            try
            {
                var featMgr = doc.FeatureManager;
                if (featMgr != null)
                {
                    object features = null;
                    try { features = featMgr.GetFeatures(false); } catch { /* ignore */ }

                    if (features is Array arr)
                    {
                        info.FeatureCount = arr.Length;
                        info.HasFeatureCount = true;
                    }
                    else if (features is object[] objArr)
                    {
                        info.FeatureCount = objArr.Length;
                        info.HasFeatureCount = true;
                    }
                }
            }
            catch { /* ignore */ }

            // Material — PartDoc only
            try
            {
                if (doc is PartDoc partDoc)
                {
                    try
                    {
                        string dummy = null;
                        string mat = partDoc.GetMaterialPropertyName2("", out dummy);
                        if (!string.IsNullOrWhiteSpace(mat))
                        {
                            info.MaterialName = mat.Trim();
                            info.HasMaterial = true;
                        }
                    }
                    catch { /* ignore — can fail on default templates */ }
                }
            }
            catch { /* ignore */ }

            return info;
        }

        private static string SafeTitle(ModelDoc2 doc)
        {
            try { return doc.GetTitle(); } catch { return null; }
        }

        private static string SafePathName(ModelDoc2 doc)
        {
            try { return doc.GetPathName(); } catch { return null; }
        }

        private static int SafeDocType(ModelDoc2 doc)
        {
            try { return doc.GetType(); } catch { return -1; }
        }
    }
}
