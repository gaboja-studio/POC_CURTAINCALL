using CurtainCall.Settings;
using UnityEngine;

namespace CurtainCall.WorldIndicators
{
    public static class WorldIndicatorFactory
    {
        public static AreaIndicatorView Area(Transform owner,bool circular) => Create(circular?GameSettings.WorldUi.circlePrefab:GameSettings.WorldUi.rectanglePrefab,owner);
        public static SweptVolumeIndicatorView Volume(Transform owner) => Create(GameSettings.WorldUi.volumePrefab,owner);
        public static PathIndicatorView Path(Transform owner) => Create(GameSettings.WorldUi.pathPrefab,owner);
        public static TrajectoryIndicatorView Trajectory(Transform owner) => Create(GameSettings.WorldUi.trajectoryPrefab,owner);
        public static TargetIndicatorView Target(Transform owner) => Create(GameSettings.WorldUi.targetPrefab,owner);
        static T Create<T>(T prefab,Transform owner) where T:WorldIndicatorView
        {
            if(!prefab || !GameSettings.WorldUi.style) return null;
            var view=Object.Instantiate(prefab,owner); view.name=prefab.name; view.Hide(); return view;
        }
    }
}
