// Only compiled when SolidWorks is NOT installed (SolidWorksInteropPath not set).
// Shims let `dotnet build` pass in CI / on machines without SolidWorks — real interop is
// excluded via Compile Remove when SolidWorksInteropPath IS set.
// Mirrors REAL signatures discovered via reflection from
// D:\SOLIDWORKS Corp\SOLIDWORKS\api\redist\SolidWorks.Interop.sldworks.dll:
//
//   ISldWorks.CreateTaskpaneView3(object ImageList, string ToolTip) -> ITaskpaneView
//   IModelDocExtension.NeedsRebuild (property, not GetRebuildFlag)
//   ITaskpaneView.DeleteView() (not ISldWorks.DeleteTaskpaneView2)

using System;

#pragma warning disable CS1591, IDE1006, CA1707, CA1711, CS0649

namespace SolidWorks.Interop.sldworks
{
    public class ModelDoc2
    {
        public virtual string GetTitle() => null;
        public virtual string GetPathName() => null;
        public new virtual int GetType() => 0;
        public virtual bool GetSaveFlag() => false;
        public virtual object FeatureManagerRaw => null;
        public IFeatureManager FeatureManager => null;
        public ModelDocExtension Extension => new ModelDocExtension();
    }

    public class PartDoc : ModelDoc2
    {
        public string GetMaterialPropertyName2(string configName, out string displayState)
        {
            displayState = null;
            return null;
        }
    }

    public interface IFeatureManager
    {
        object GetFeatures(bool topLevelOnly);
    }

    public class ModelDocExtension
    {
        public bool NeedsRebuild => false;
    }

    public interface ISldWorks
    {
        void SetAddinCallbackInfo2(int unused1, object callback, int cookie);
        object ActiveDoc { get; }
        ITaskpaneView CreateTaskpaneView3(object imageList, string toolTip);
        ITaskpaneView CreateTaskpaneView2(string bitmap, string toolTip);
    }

    public interface ISwAddin
    {
        bool ConnectToSW(object ThisSW, int Cookie);
        bool DisconnectFromSW();
    }

    public interface ITaskpaneView
    {
        object AddControl(string progId, string licenseKey);
        bool DeleteView();
    }

    public class SldWorks : ISldWorks
    {
        public void SetAddinCallbackInfo2(int unused1, object callback, int cookie) { }
        public object ActiveDoc => null;
        public ITaskpaneView CreateTaskpaneView3(object imageList, string toolTip) => null;
        public ITaskpaneView CreateTaskpaneView2(string bitmap, string toolTip) => null;
    }
}

namespace SolidWorks.Interop.swpublished { }

namespace SolidWorks.Interop.swconst
{
    public enum swDocumentTypes_e
    {
        swDocNONE = 0,
        swDocPART = 1,
        swDocASSEMBLY = 2,
        swDocDRAWING = 3
    }
}

#pragma warning restore CS1591, IDE1006, CA1707, CA1711, CS0649
