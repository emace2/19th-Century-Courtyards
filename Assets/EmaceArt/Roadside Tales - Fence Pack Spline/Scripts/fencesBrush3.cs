// EasyFenceTool — v2.1
using UnityEngine;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace EasyFence.Editor
{
    public enum PostPlacementMode { AllPoints, EndsOnly, CornersOnly }
    public enum OffsetMode { Standard, Freeform }

    [ExecuteInEditMode]
    [HelpURL("https://your-docs-link.com")]
    public class EasyFenceTool : MonoBehaviour
    {
        [HideInInspector] public Texture2D ToolLogo;
        
        // --- FENCE SEGMENTS DATA ---
        [HideInInspector] public int FenceSourceMode = 0;
        [HideInInspector] public List<GameObject> FencePrefabs = new List<GameObject>();
        [HideInInspector] public List<GameObject> ExcludedFencePrefabs = new List<GameObject>();
        [HideInInspector] public List<string> FencePrefabWeightIds = new List<string>();
        [HideInInspector] public List<float>  FencePrefabWeightValues = new List<float>();
        [HideInInspector] public List<string> FencePrefabAlwaysVerticalIds = new List<string>();

#if UNITY_EDITOR
        [HideInInspector] public DefaultAsset FenceFolder;
        private DefaultAsset _lastCheckedFenceFolder;
        private List<GameObject> _cachedFenceFolderPrefabs = new List<GameObject>();
#else
        [HideInInspector] public Object FenceFolder;
#endif

        // --- POSTS DATA ---
        [HideInInspector] public int PostSourceMode = 0;
        [HideInInspector] public List<GameObject> PostPrefabs = new List<GameObject>();
        [HideInInspector] public List<GameObject> ExcludedPostPrefabs = new List<GameObject>();
        [HideInInspector] public List<string> PostPrefabWeightIds = new List<string>();
        [HideInInspector] public List<float>  PostPrefabWeightValues = new List<float>();
        [HideInInspector] public List<string> PostPrefabAlwaysVerticalIds = new List<string>();

#if UNITY_EDITOR
        [HideInInspector] public DefaultAsset PostFolder;
        private DefaultAsset _lastCheckedPostFolder;
        private List<GameObject> _cachedPostFolderPrefabs = new List<GameObject>();
#else
        [HideInInspector] public Object PostFolder;
#endif

        public PostPlacementMode PlacementMode = PostPlacementMode.AllPoints;
        
        [Range(1f, 180f)] 
        [Tooltip("Minimum angle deviation required to place a post in Corners Only mode.")]
        public float CornerAngleThreshold = 15f;
        
        [Tooltip("Independent vertical offset for posts to sink or raise them relative to segments.")]
        public float PostVerticalOffset = 0f;

        [Tooltip("Automatically calculates the precise length of each prefab based on its mesh bounds. This prevents overlapping and guarantees objects perfectly touch end-to-end!")]
        public bool AutoCalculateLength = true;

        [Tooltip("Used ONLY if Auto Calculate Length is FALSE. Prevent overlap crashes.")]
        public float SegmentLength = 2.5f;

        [Tooltip("Rotation offset added to each fence segment.")]
        public Vector3 RotationOffset = new Vector3(0, 90, 0);

        [Tooltip("Position offset added to each fence segment relative to the path.")]
        public Vector3 PositionOffset = Vector3.zero;

        // --- POST NATURAL VARIATION ---
        [Range(0f, 1f)]
        [Tooltip("Chance (0-1) that a post won't be generated, simulating broken or missing posts.")]
        public float PostMissingChance = 0f;

        [Tooltip("Random angular rotation applied to each post on all three axes.")]
        public Vector3 PostRandomRotationJitter = Vector3.zero;

        [Tooltip("Minimum and maximum random scale multipliers applied to posts.")]
        public Vector2 PostRandomScaleRange = new Vector2(1f, 1f);

        // --- POST PLACEMENT & GEOMETRY ---
        [Tooltip("Rotation offset added to each post (on top of the prefab's own rotation).")]
        public Vector3 PostRotationOffset = Vector3.zero;

        [Tooltip("Position offset added to each post relative to its path point.")]
        public Vector3 PostPositionOffset = Vector3.zero;

        // --- QUICK SHAPES ---
        [HideInInspector] public float ShapeSize       = 5f;
        [HideInInspector] public int   ShapeResolution = 16;
        [HideInInspector] public bool  ShapeActive     = false;
        [HideInInspector] public bool  ShapeIsCircle   = true;

        // --- FORWARD AXIS & OFFSET MODE ---
        [HideInInspector] public OffsetMode FenceOffsetMode = OffsetMode.Standard;
        [HideInInspector] public OffsetMode PostOffsetMode  = OffsetMode.Standard;

        [Header("Terrain Adaptation")]
        [Tooltip("Keeps segments globally upright (world Y-up), regardless of slope. When ON: segments don't tilt with terrain — ideal for wooden planks and panels. When OFF: segments follow the slope angle. Note: X and Z rotation jitter (Natural Variation) are automatically suppressed when this is ON.")]
        public bool KeepVertical = true;

        [Tooltip("Snaps each segment, post, and path point to the physical terrain/colliders.")]
        public bool SnapToTerrain = true;

        [Tooltip("If true, ignores standard meshes and ONLY snaps to Unity Terrain objects.")]
        public bool SnapOnlyToUnityTerrain = true;

        [Tooltip("Layer mask used for terrain snapping raycasts.")]
        public LayerMask TerrainMask = ~0;

        [Range(0f, 90f)]
        [Tooltip("Maximum surface angle (from horizontal) accepted for terrain snapping. Surfaces steeper than this are ignored.")]
        public float SlopeLimit = 90f;

        [Tooltip("Vertical offset on the Y axis to precisely sink or raise assets relative to the ground.")]
        public float VerticalOffset = 0f;

        [Range(0f, 1f)]
        [Tooltip("Chance (0-1) that a segment won't generate, simulating broken or missing fence pieces.")]
        public float MissingSegmentChance = 0f;

        [Tooltip("Random angular rotation applied to each segment. X and Z axes are suppressed when Keep Vertical is ON — only Y (yaw/spin) applies in that mode.")]
        public Vector3 RandomRotationJitter = Vector3.zero;

        [Tooltip("Minimum and maximum random scale multipliers.")]
        public Vector2 RandomScaleRange = new Vector2(1f, 1f);

        [Header("Path Options")]
        [Tooltip("Connects the last point back to the first one.")]
        public bool IsLoop = false;

        [Min(0.1f)] 
        [Tooltip("Distance threshold for inserting a new point between existing ones.")]
        public float InsertThreshold = 1.5f;

        [HideInInspector] public int RandomSeed     = 0;
        [HideInInspector] public int PostRandomSeed = 0;
        [HideInInspector] public List<Vector3> Points = new List<Vector3>();

#if UNITY_EDITOR
        [HideInInspector] [SerializeField] private GameObject previewContainer;
        private bool _needsRebuild = true;

        private void OnDrawGizmos()
        {
            if (Points == null || Points.Count == 0) return;

            Gizmos.color = new Color(0, 0.9f, 1f, 0.7f);
            for (int i = 0; i < Points.Count; i++)
            {
                Vector3 p1 = SnapToTerrain ? GetGroundPos(Points[i]) : Points[i];
                Gizmos.DrawWireSphere(p1, 0.18f);

                if (i < Points.Count - 1)
                {
                    Vector3 p2 = SnapToTerrain ? GetGroundPos(Points[i + 1]) : Points[i + 1];
                    Gizmos.DrawLine(p1, p2);
                }
                else if (IsLoop && Points.Count > 2)
                {
                    Vector3 p2 = SnapToTerrain ? GetGroundPos(Points[0]) : Points[0];
                    Gizmos.DrawLine(p1, p2);
                }
            }
        }

        private double _lastTerrainCheckTime = 0;
        private float[] _cachedHeights = new float[0];
        private RaycastHit[] _raycastBuffer = new RaycastHit[16];

        private void OnValidate()
        {
            _lastCheckedFenceFolder = null; 
            _lastCheckedPostFolder = null;
            TriggerRebuild();
        }

        private void Update()
        {
            if (Application.isPlaying) return;

            // Points are in world space. Rebuild is triggered when transform changes (e.g. via parent).
            if (transform.hasChanged)
            {
                _needsRebuild        = true;
                transform.hasChanged = false;
            }

            if (_needsRebuild)
            {
                RebuildPreview();
                _needsRebuild = false;
            }
            else if (SnapToTerrain && Points.Count > 0)
            {
                if (EditorApplication.timeSinceStartup - _lastTerrainCheckTime > 0.15f)
                {
                    _lastTerrainCheckTime = EditorApplication.timeSinceStartup;
                    if (CheckTerrainModifications()) TriggerRebuild();
                }
            }
        }

        private bool CheckTerrainModifications()
        {
            int segs = IsLoop ? Points.Count : Points.Count - 1;
            int totalChecks = Points.Count + Mathf.Max(0, segs);

            if (_cachedHeights == null || _cachedHeights.Length != totalChecks)
            {
                UpdateCachedHeights(totalChecks, segs);
                return false;
            }

            int index = 0;
            for (int i = 0; i < Points.Count; i++)
            {
                if (Mathf.Abs(_cachedHeights[index++] - GetGroundPos(Points[i]).y) > 0.05f)
                {
                    UpdateCachedHeights(totalChecks, segs);
                    return true;
                }
            }

            for (int i = 0; i < segs; i++)
            {
                Vector3 p1 = SnapToTerrain ? GetGroundPos(Points[i]) : Points[i];
                Vector3 p2 = SnapToTerrain ? GetGroundPos(Points[(i + 1) % Points.Count]) : Points[(i + 1) % Points.Count];
                Vector3 mid = (p1 + p2) * 0.5f;
                
                if (Mathf.Abs(_cachedHeights[index++] - GetGroundPos(mid).y) > 0.05f)
                {
                    UpdateCachedHeights(totalChecks, segs);
                    return true;
                }
            }
            return false;
        }

        private void UpdateCachedHeights(int totalChecks, int segs)
        {
            _cachedHeights = new float[totalChecks];
            int index = 0;
            for (int i = 0; i < Points.Count; i++)
            {
                _cachedHeights[index++] = GetGroundPos(Points[i]).y;
            }
            for (int i = 0; i < segs; i++)
            {
                Vector3 p1 = SnapToTerrain ? GetGroundPos(Points[i]) : Points[i];
                Vector3 p2 = SnapToTerrain ? GetGroundPos(Points[(i + 1) % Points.Count]) : Points[(i + 1) % Points.Count];
                Vector3 mid = (p1 + p2) * 0.5f;
                _cachedHeights[index++] = GetGroundPos(mid).y;
            }
        }

        private void OnEnable()
        {
            if (RandomSeed     == 0) RandomSeed     = UnityEngine.Random.Range(1, int.MaxValue);
            if (PostRandomSeed == 0) PostRandomSeed = UnityEngine.Random.Range(1, int.MaxValue);
            TriggerRebuild();
        }

        private void OnDisable() => ClearPreview();
        private void OnDestroy() => ClearPreview();

        public void TriggerRebuild() => _needsRebuild = true;

        public void ClearPreview()
        {
            if (previewContainer != null) DestroyImmediate(previewContainer);
            
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform child = transform.GetChild(i);
                if (child.name == "EasyFenceTool_Preview") DestroyImmediate(child.gameObject);
            }
        }

        public Vector3 GetGroundPos(Vector3 pos)
        {
            int count = Physics.RaycastNonAlloc(pos + Vector3.up * 50f, Vector3.down, _raycastBuffer, 100f, TerrainMask, QueryTriggerInteraction.Ignore);

            float bestY = float.MinValue;
            bool found = false;

            for (int i = 0; i < count; i++)
            {
                var hit = _raycastBuffer[i];
                if (SnapOnlyToUnityTerrain && !(hit.collider is TerrainCollider)) continue;
                if (Vector3.Angle(hit.normal, Vector3.up) > SlopeLimit) continue;

                if (hit.point.y > bestY)
                {
                    bestY = hit.point.y;
                    found = true;
                }
            }

            return found ? new Vector3(pos.x, bestY, pos.z) : pos;
        }

        public float GetPhysicalMinLength()
        {
            var prefabs = GetAllSourceFencePrefabs();
            if (prefabs.Count == 0) return 0.1f;

            float minLen = float.MaxValue;
            foreach (var p in prefabs)
            {
                if (p == null) continue;
                var mf = p.GetComponentInChildren<MeshFilter>();
                if (mf && mf.sharedMesh)
                {
                    Vector3 size = mf.sharedMesh.bounds.size;
                    Vector3 scl = p.transform.localScale;
                    float len = Mathf.Max(size.x * scl.x, size.z * scl.z);
                    if (len > 0.05f && len < minLen) minLen = len;
                }
            }
            return (minLen == float.MaxValue || minLen < 0.1f) ? 0.1f : minLen;
        }

        // Returns the LONGEST fence prefab dimension — used for circle constraints so that
        // the default radius guarantees ALL panels (not just the smallest) fit in each segment.
        public float GetPhysicalMaxLength()
        {
            var prefabs = GetAllSourceFencePrefabs();
            if (prefabs.Count == 0) return 2f;

            float maxLen = 0f;
            foreach (var p in prefabs)
            {
                if (p == null) continue;
                var mf = p.GetComponentInChildren<MeshFilter>();
                if (mf && mf.sharedMesh)
                {
                    Vector3 size = mf.sharedMesh.bounds.size;
                    Vector3 scl  = p.transform.localScale;
                    float   len  = Mathf.Max(size.x * scl.x, size.z * scl.z);
                    if (len > maxLen) maxLen = len;
                }
            }
            return maxLen > 0.1f ? maxLen : 2f;
        }

        public float GetPrefabLength(GameObject prefab, float scaleMultiplier)
        {
            if (!AutoCalculateLength) return Mathf.Max(GetPhysicalMinLength(), SegmentLength) * scaleMultiplier;
            
            var mf = prefab.GetComponentInChildren<MeshFilter>();
            if (mf && mf.sharedMesh)
            {
                Vector3 size = mf.sharedMesh.bounds.size;
                Vector3 scl = prefab.transform.localScale;
                return Mathf.Max(size.x * scl.x, size.z * scl.z) * scaleMultiplier;
            }
            return Mathf.Max(GetPhysicalMinLength(), SegmentLength) * scaleMultiplier;
        }

        public float GetPostRadius(GameObject prefab)
        {
            if (prefab == null) return 0f;
            var mf = prefab.GetComponentInChildren<MeshFilter>();
            if (mf && mf.sharedMesh)
            {
                Vector3 size = mf.sharedMesh.bounds.size;
                Vector3 scl = prefab.transform.localScale;
                return Mathf.Max(size.x * scl.x, size.z * scl.z) * 0.5f;
            }
            return 0.25f;
        }

        public float GetMaxPostRadius()
        {
            var activePosts = GetActivePostPrefabs();
            if (activePosts.Count == 0) return 0f;
            
            float maxR = 0f;
            foreach(var p in activePosts)
            {
                float r = GetPostRadius(p);
                if (r > maxR) maxR = r;
            }
            return maxR;
        }

        public bool ShouldPlacePost(int index, int totalPoints, bool isLoop)
        {
            if (PlacementMode == PostPlacementMode.AllPoints) return true;
            if (PlacementMode == PostPlacementMode.EndsOnly)
            {
                if (isLoop) return index == 0; 
                return index == 0 || index == totalPoints - 1;
            }
            if (PlacementMode == PostPlacementMode.CornersOnly)
            {
                if (!isLoop && (index == 0 || index == totalPoints - 1)) return true;
                
                int prevIdx = (index - 1 + totalPoints) % totalPoints;
                int nextIdx = (index + 1) % totalPoints;
                
                Vector3 prev = Points[prevIdx];
                Vector3 curr = Points[index];
                Vector3 next = Points[nextIdx];
                
                if (SnapToTerrain) 
                {
                    prev = GetGroundPos(prev);
                    curr = GetGroundPos(curr);
                    next = GetGroundPos(next);
                }

                Vector3 dir1 = (curr - prev).normalized;
                Vector3 dir2 = (next - curr).normalized;
                
                if (KeepVertical) 
                { 
                    dir1.y = 0; dir2.y = 0; 
                    dir1.Normalize(); dir2.Normalize(); 
                }
                
                float angle = Vector3.Angle(dir1, dir2);
                return angle >= CornerAngleThreshold;
            }
            return true;
        }

        public bool IsFencePrefabAlwaysVertical(GameObject prefab)
        {
            string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(prefab));
            return FencePrefabAlwaysVerticalIds.Contains(guid);
        }

        public bool IsPostPrefabAlwaysVertical(GameObject prefab)
        {
            string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(prefab));
            return PostPrefabAlwaysVerticalIds.Contains(guid);
        }

        public float GetFencePrefabWeight(GameObject prefab)
        {
            string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(prefab));
            int idx = FencePrefabWeightIds.IndexOf(guid);
            return idx >= 0 ? Mathf.Max(0f, FencePrefabWeightValues[idx]) : 100f;
        }

        public float GetPostPrefabWeight(GameObject prefab)
        {
            string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(prefab));
            int idx = PostPrefabWeightIds.IndexOf(guid);
            return idx >= 0 ? Mathf.Max(0f, PostPrefabWeightValues[idx]) : 100f;
        }

        public static GameObject PickWeighted(List<GameObject> prefabs, System.Func<GameObject, float> getWeight)
        {
            float total = 0f;
            foreach (var p in prefabs) total += getWeight(p);
            if (total <= 0f) return prefabs[0];
            float r = UnityEngine.Random.value * total;
            float cumul = 0f;
            foreach (var p in prefabs)
            {
                cumul += getWeight(p);
                if (r <= cumul) return p;
            }
            return prefabs[prefabs.Count - 1];
        }

        public void RebuildPreview()
        {
            ClearPreview();

            if (SnapToTerrain && Points.Count > 0)
            {
                for (int i = 0; i < Points.Count; i++)
                {
                    Points[i] = GetGroundPos(Points[i]);
                }
            }

            var fencePrefabs = GetActiveFencePrefabs();
            var postPrefabs = GetActivePostPrefabs();

            if (Points.Count < 1) return;
            if (fencePrefabs.Count == 0 && postPrefabs.Count == 0) return;

            previewContainer = new GameObject("EasyFenceTool_Preview");
            previewContainer.transform.SetParent(this.transform, false);
            previewContainer.hideFlags = HideFlags.HideAndDontSave;

            UnityEngine.Random.State oldState = UnityEngine.Random.state;
            float postRadius = GetMaxPostRadius();

            if (postPrefabs.Count > 0 && Points.Count > 0)
            {
                for (int i = 0; i < Points.Count; i++)
                {
                    if (!ShouldPlacePost(i, Points.Count, IsLoop)) continue;

                    UnityEngine.Random.InitState((i * 54321) ^ PostRandomSeed);

                    if (PostMissingChance > 0f && UnityEngine.Random.value < PostMissingChance) continue;

                    GameObject sourcePost = PickWeighted(postPrefabs, GetPostPrefabWeight);
                    if (!sourcePost) continue;

                    float pScale = UnityEngine.Random.Range(PostRandomScaleRange.x, PostRandomScaleRange.y);
                    Quaternion postRot = sourcePost.transform.rotation * Quaternion.Euler(PostRotationOffset);
                    postRot *= Quaternion.Euler(
                        UnityEngine.Random.Range(-PostRandomRotationJitter.x, PostRandomRotationJitter.x),
                        UnityEngine.Random.Range(-PostRandomRotationJitter.y, PostRandomRotationJitter.y),
                        UnityEngine.Random.Range(-PostRandomRotationJitter.z, PostRandomRotationJitter.z)
                    );

                    Vector3 pos = Points[i];
                    if (SnapToTerrain) pos = GetGroundPos(pos);
                    pos.y += VerticalOffset + PostVerticalOffset;
                    if (PostOffsetMode == OffsetMode.Standard)
                    {
                        // Standard = world-space offset: X = world X, Y = world Y, Z = world Z.
                        pos += PostPositionOffset;
                    }
                    else
                    {
                        pos += postRot * PostPositionOffset;
                    }

                    var mfPost = sourcePost.GetComponentInChildren<MeshFilter>();
                    var mrPost = sourcePost.GetComponentInChildren<MeshRenderer>();

                    if (mfPost && mrPost)
                    {
                        GameObject visual = new GameObject("PreviewPost");
                        visual.transform.SetParent(previewContainer.transform, false);
                        visual.transform.position = pos;
                        visual.transform.rotation = postRot;
                        visual.transform.localScale = sourcePost.transform.localScale * pScale;
                        visual.hideFlags = HideFlags.HideAndDontSave;

                        visual.AddComponent<MeshFilter>().sharedMesh = mfPost.sharedMesh;
                        visual.AddComponent<MeshRenderer>().sharedMaterials = mrPost.sharedMaterials;
                    }
                }
            }

            if (Points.Count >= 2 && fencePrefabs.Count > 0)
            {
                int segs = IsLoop ? Points.Count : Points.Count - 1;

                for (int i = 0; i < segs; i++)
                {
                    Vector3 a = SnapToTerrain ? GetGroundPos(Points[i]) : Points[i];
                    Vector3 b = SnapToTerrain ? GetGroundPos(Points[(i + 1) % Points.Count]) : Points[(i + 1) % Points.Count];

                    Vector3 dir = (b - a);
                    if (KeepVertical) dir.y = 0;

                    float totalDist = dir.magnitude;
                    if (totalDist < 0.05f) continue;
                    dir.Normalize();

                    bool hasStartPost = ShouldPlacePost(i, Points.Count, IsLoop);
                    bool hasEndPost = ShouldPlacePost((i + 1) % Points.Count, Points.Count, IsLoop);

                    float startOffset = hasStartPost ? postRadius : 0f;
                    float endOffset = hasEndPost ? postRadius : 0f;

                    float currentDist = startOffset;
                    float maxDist = totalDist - endOffset;
                    int j = 0;

                    while (currentDist < maxDist)
                    {
                        UnityEngine.Random.InitState((i * 73856) ^ (j * 19349) ^ RandomSeed);
                        j++;
                        if (j > 1000) break; 

                        GameObject sourcePrefab = PickWeighted(fencePrefabs, GetFencePrefabWeight);
                        if (!sourcePrefab) break;

                        float sScale = UnityEngine.Random.Range(RandomScaleRange.x, RandomScaleRange.y);
                        float actualLength = GetPrefabLength(sourcePrefab, sScale);
                        if (actualLength < 0.05f) actualLength = 0.5f; 

                        if (currentDist + actualLength > maxDist + 0.02f) break;

                        if (MissingSegmentChance > 0f && UnityEngine.Random.value < MissingSegmentChance)
                        {
                            currentDist += actualLength; 
                            continue;
                        }

                        var mf = sourcePrefab.GetComponentInChildren<MeshFilter>();
                        var mr = sourcePrefab.GetComponentInChildren<MeshRenderer>();
                        if (!mf || !mr || !mf.sharedMesh) 
                        {
                            currentDist += actualLength;
                            continue;
                        }

                        Vector3 basePos = a + dir * (currentDist + actualLength * 0.5f);
                        if (SnapToTerrain) basePos = GetGroundPos(basePos);
                        basePos.y += VerticalOffset;

                        bool forceVert = KeepVertical || IsFencePrefabAlwaysVertical(sourcePrefab);
                        Vector3 rotDir = forceVert ? new Vector3(dir.x, 0f, dir.z).normalized : dir;
                        Quaternion rot = Quaternion.LookRotation(rotDir) * Quaternion.Euler(RotationOffset);
                        rot *= Quaternion.Euler(
                            forceVert ? 0f : UnityEngine.Random.Range(-RandomRotationJitter.x, RandomRotationJitter.x),
                            UnityEngine.Random.Range(-RandomRotationJitter.y, RandomRotationJitter.y),
                            forceVert ? 0f : UnityEngine.Random.Range(-RandomRotationJitter.z, RandomRotationJitter.z)
                        );

                        Vector3 pos;
                        if (FenceOffsetMode == OffsetMode.Standard)
                        {
                            // Standard = world-space offset: X always = world X, Y = world Y, Z = world Z.
                            // No rotation applied — avoids axis swapping and fanning on curved paths.
                            pos = basePos + PositionOffset;
                        }
                        else
                        {
                            pos = basePos + rot * PositionOffset;
                        }

                        GameObject visual = new GameObject("PreviewSegment");
                        visual.transform.SetParent(previewContainer.transform, false);
                        visual.transform.position = pos;
                        visual.transform.rotation = rot;
                        visual.transform.localScale = sourcePrefab.transform.localScale * sScale;
                        visual.hideFlags = HideFlags.HideAndDontSave;

                        visual.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
                        visual.AddComponent<MeshRenderer>().sharedMaterials = mr.sharedMaterials;

                        currentDist += actualLength; 
                    }
                }
            }
            UnityEngine.Random.state = oldState;
        }

        private List<GameObject> LoadFolderPrefabs(ref DefaultAsset lastFolder, ref List<GameObject> folderCache, DefaultAsset folder)
        {
            if (folder != lastFolder || folderCache.Count == 0)
            {
                folderCache.Clear();
                string path = AssetDatabase.GetAssetPath(folder);
                if (!string.IsNullOrEmpty(path))
                {
                    foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { path }))
                    {
                        var go = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                        if (go != null) folderCache.Add(go);
                    }
                }
                lastFolder = folder;
            }
            return folderCache;
        }

        public List<GameObject> GetAllSourceFencePrefabs()
        {
            var all = new List<GameObject>();
            if (FenceSourceMode == 0)
            {
                if (FencePrefabs != null) all.AddRange(FencePrefabs);
            }
            else if (FenceFolder != null)
                all.AddRange(LoadFolderPrefabs(ref _lastCheckedFenceFolder, ref _cachedFenceFolderPrefabs, FenceFolder));
            else { _lastCheckedFenceFolder = null; _cachedFenceFolderPrefabs.Clear(); }
            all.RemoveAll(x => x == null);
            return all;
        }

        public List<GameObject> GetActiveFencePrefabs()
        {
            var active = GetAllSourceFencePrefabs();
            if (ExcludedFencePrefabs != null && ExcludedFencePrefabs.Count > 0)
                active.RemoveAll(x => ExcludedFencePrefabs.Contains(x));
            return active;
        }

        public List<GameObject> GetAllSourcePostPrefabs()
        {
            var all = new List<GameObject>();
            if (PostSourceMode == 0)
            {
                if (PostPrefabs != null) all.AddRange(PostPrefabs);
            }
            else if (PostFolder != null)
                all.AddRange(LoadFolderPrefabs(ref _lastCheckedPostFolder, ref _cachedPostFolderPrefabs, PostFolder));
            else { _lastCheckedPostFolder = null; _cachedPostFolderPrefabs.Clear(); }
            all.RemoveAll(x => x == null);
            return all;
        }

        public List<GameObject> GetActivePostPrefabs()
        {
            var active = GetAllSourcePostPrefabs();
            if (ExcludedPostPrefabs != null && ExcludedPostPrefabs.Count > 0)
                active.RemoveAll(x => ExcludedPostPrefabs.Contains(x));
            return active;
        }
#endif
    }

#if UNITY_EDITOR
    [InitializeOnLoad]
    [CustomEditor(typeof(EasyFenceTool)), CanEditMultipleObjects]
    public class EasyFenceToolEditor : UnityEditor.Editor
    {
        private bool _isEditing = true;
        private Texture2D _headerImage;
        private GUIStyle _dropStyle;

        private static int   s_DragId;
        private static float s_DragAccum;
        private static int   s_DragOrigin;
        private GUIStyle     _weightLabelStyle;

        // Index of the point whose PositionHandle is currently being dragged (-1 = none)
        private int _activeHandleIdx = -1;

        // Ghost point shown under cursor when path is empty
        private Vector3 _ghostPos   = Vector3.zero;
        private bool    _ghostValid = false;

        static EasyFenceToolEditor()
        {
            SceneView.duringSceneGui -= StaticScenePicker;
            SceneView.duringSceneGui += StaticScenePicker;
        }

        private static void StaticScenePicker(SceneView sv)
        {
            Event e = Event.current;
            if (e.type != EventType.MouseDown || e.button != 0 || e.shift || e.control || e.alt)
                return;

            // Don't interfere when clicking near an already-selected fence's points
            foreach (var sel in Selection.gameObjects)
            {
                var selTool = sel.GetComponent<EasyFenceTool>();
                if (selTool == null || selTool.Points == null) continue;
                for (int k = 0; k < selTool.Points.Count; k++)
                {
                    Vector3 wp = selTool.SnapToTerrain ? selTool.GetGroundPos(selTool.Points[k]) : selTool.Points[k];
                    if (Vector2.Distance(HandleUtility.WorldToGUIPoint(wp), e.mousePosition) < 50f)
                        return;
                }
            }

            const float kPixels = 30f;
            float       minDist = kPixels;
            EasyFenceTool closest = null;

            foreach (var tool in Object.FindObjectsOfType<EasyFenceTool>())
            {
                if (Selection.Contains(tool.gameObject)) continue;
                if (tool.Points == null || tool.Points.Count == 0) continue;
                for (int j = 0; j < tool.Points.Count; j++)
                {
                    Vector3 wp = tool.SnapToTerrain ? tool.GetGroundPos(tool.Points[j]) : tool.Points[j];
                    float d = Vector2.Distance(HandleUtility.WorldToGUIPoint(wp), e.mousePosition);
                    if (d < minDist) { minDist = d; closest = tool; }
                }
            }

            if (closest != null)
            {
                Selection.activeGameObject = closest.gameObject;
                e.Use();
            }
        }

        // Handles drag on any Rect — returns new value (0-100, step 15)
        private static int HandleWeightDrag(Rect r, int value)
        {
            int id = GUIUtility.GetControlID(FocusType.Passive, r);
            Event e = Event.current;

            if (e.type == EventType.MouseDown && e.button == 0 && r.Contains(e.mousePosition))
            {
                s_DragId     = id;
                s_DragAccum  = 0f;
                s_DragOrigin = value;
            }

            if (e.type == EventType.MouseDrag && e.button == 0 && s_DragId == id)
            {
                s_DragAccum += e.delta.x;
                value = Mathf.Clamp(s_DragOrigin + Mathf.RoundToInt(s_DragAccum / 5f) * 15, 0, 100);
                GUI.changed = true;
                GUIUtility.hotControl      = id;
                GUIUtility.keyboardControl = 0;
                e.Use();
            }

            if (s_DragId == id && (e.type == EventType.MouseUp || e.type == EventType.Ignore))
            {
                s_DragId = 0;
                if (GUIUtility.hotControl == id) GUIUtility.hotControl = 0;
            }

            return value;
        }

        // --- Float / step drag statics (shared, only one drag active at a time) ---
        private static int   s_FloatDragId;
        private static float s_FloatDragAccum;
        private static float s_FloatDragOriginF;

        private static float HandleFloatDrag(Rect r, float value, float step, float min, float max = float.MaxValue)
        {
            int id = GUIUtility.GetControlID(FocusType.Passive, r);
            Event e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 0 && r.Contains(e.mousePosition))
            { s_FloatDragId = id; s_FloatDragAccum = 0f; s_FloatDragOriginF = value; }
            if (e.type == EventType.MouseDrag && e.button == 0 && s_FloatDragId == id)
            {
                s_FloatDragAccum += e.delta.x;
                value = Mathf.Clamp(s_FloatDragOriginF + s_FloatDragAccum * step, min, max);
                GUI.changed = true; GUIUtility.hotControl = id; GUIUtility.keyboardControl = 0; e.Use();
            }
            if (s_FloatDragId == id && (e.type == EventType.MouseUp || e.type == EventType.Ignore))
            { s_FloatDragId = 0; if (GUIUtility.hotControl == id) GUIUtility.hotControl = 0; }
            return value;
        }

        private static int HandleIntStepDrag(Rect r, int value, int[] steps)
        {
            int id = GUIUtility.GetControlID(FocusType.Passive, r);
            Event e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 0 && r.Contains(e.mousePosition))
            { s_FloatDragId = id; s_FloatDragAccum = 0f; s_FloatDragOriginF = value; }
            if (e.type == EventType.MouseDrag && e.button == 0 && s_FloatDragId == id)
            {
                s_FloatDragAccum += e.delta.x;
                int originIdx = System.Array.IndexOf(steps, (int)s_FloatDragOriginF);
                if (originIdx < 0) originIdx = 1;
                int newIdx = Mathf.Clamp(originIdx + Mathf.RoundToInt(s_FloatDragAccum / 20f), 0, steps.Length - 1);
                value = steps[newIdx];
                GUI.changed = true; GUIUtility.hotControl = id; GUIUtility.keyboardControl = 0; e.Use();
            }
            if (s_FloatDragId == id && (e.type == EventType.MouseUp || e.type == EventType.Ignore))
            { s_FloatDragId = 0; if (GUIUtility.hotControl == id) GUIUtility.hotControl = 0; }
            return value;
        }

        // Minimum circle radius for given pts so that chord fits at least one fence segment.
        // chord = 2r*sin(π/pts) ≥ needed  →  r ≥ needed / (2*sin(π/pts))
        // Hard minimum for circles is 10 units.
        private static float MinRadiusForPts(int pts, float minSegLen, float postR)
        {
            if (minSegLen <= 0f || pts <= 0) return 10f;
            float s = Mathf.Sin(Mathf.PI / pts);
            return s < 0.0001f ? 10f : Mathf.Max(10f, (minSegLen + postR * 2f) / (2f * s));
        }

        // Maximum pts (snapped DOWN to nearest step) so that chord stays long enough.
        // More pts → shorter chord. chord = 2r*sin(π/pts) ≥ needed  →  pts ≤ π/arcsin(needed/(2r))
        private static int MaxPtsForRadius(float r, float minSegLen, float postR, int[] steps)
        {
            if (r <= 0f || minSegLen <= 0f) return steps[steps.Length - 1]; // no constraint
            float sinNeeded = (minSegLen + postR * 2f) / (2f * r);
            if (sinNeeded >= 1f) return steps[0]; // radius too small, force fewest pts
            float maxPtsF = Mathf.PI / Mathf.Asin(sinNeeded);
            for (int i = steps.Length - 1; i >= 0; i--)
                if ((float)steps[i] <= maxPtsF) return steps[i];
            return steps[0];
        }

        // Int field where the label acts as a drag handle cycling through steps
        private static int DrawIntStepField(GUIContent label, int value, int[] steps)
        {
            Rect r      = EditorGUILayout.GetControlRect(true, EditorGUIUtility.singleLineHeight);
            Rect lRect  = new Rect(r.x, r.y, EditorGUIUtility.labelWidth, r.height);
            Rect vRect  = new Rect(r.x + EditorGUIUtility.labelWidth, r.y, r.width - EditorGUIUtility.labelWidth, r.height);
            EditorGUI.LabelField(lRect, label);
            EditorGUIUtility.AddCursorRect(lRect, MouseCursor.SlideArrow);
            int newVal = HandleIntStepDrag(lRect, value, steps);
            EditorGUI.LabelField(vRect, newVal.ToString(), EditorStyles.boldLabel);
            return newVal;
        }

        // Regenerates Points[] from ShapeIsCircle/ShapeSize/ShapeResolution and queues rebuild
        private static void RegenerateAndRebuild(EasyFenceTool script)
        {
            script.Points.Clear();
            Vector3 center = script.transform.position;
            if (script.SnapToTerrain) center = script.GetGroundPos(center);
            if (script.ShapeIsCircle)
            {
                int   pts = Mathf.Max(8, script.ShapeResolution);
                float rad = Mathf.Max(10f, script.ShapeSize);
                for (int i = 0; i < pts; i++)
                {
                    float   a  = 2f * Mathf.PI * i / pts;
                    Vector3 pt = center + new Vector3(Mathf.Cos(a) * rad, 0f, Mathf.Sin(a) * rad);
                    if (script.SnapToTerrain) pt = script.GetGroundPos(pt);
                    script.Points.Add(pt);
                }
            }
            else
            {
                float half = Mathf.Max(5f, script.ShapeSize) * 0.5f;
                Vector3[] corners =
                {
                    center + new Vector3(-half, 0f, -half),
                    center + new Vector3( half, 0f, -half),
                    center + new Vector3( half, 0f,  half),
                    center + new Vector3(-half, 0f,  half),
                };
                foreach (var c in corners)
                    script.Points.Add(script.SnapToTerrain ? script.GetGroundPos(c) : c);
            }
            script.IsLoop = true;
            script.TriggerRebuild();
        }

        [MenuItem("Tools/Easy Fence/Create Fence Spline", false, 10)]
        private static void CreateFenceSpline()
        {
            var parent = new GameObject("Fence Group");
            var go     = new GameObject("Fence Spline");
            go.transform.SetParent(parent.transform, false);
            go.AddComponent<EasyFenceTool>();
            Undo.RegisterCreatedObjectUndo(parent, "Create Fence Spline");
            Selection.activeGameObject = go;
        }

        private void OnEnable()
        {
            Tools.hidden = _isEditing;
            Undo.undoRedoPerformed += OnUndoRedo;
            
            _headerImage = null;
            string[] guids = AssetDatabase.FindAssets("Label t:Texture2D");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.Contains("/img/Label."))
                {
                    _headerImage = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                    break;
                }
            }
        }

        private void OnDisable()
        {
            Tools.hidden = false;
            Undo.undoRedoPerformed -= OnUndoRedo;
        }

        private void OnUndoRedo()
        {
            foreach (var t in targets)
            {
                if (t is EasyFenceTool tool) tool.TriggerRebuild();
            }
            SceneView.RepaintAll();
        }

        private static void DrawSectionSeparator()
        {
            EditorGUILayout.Space(7);
            Rect r = GUILayoutUtility.GetRect(0f, 1f, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(r, new Color(0.4f, 0.4f, 0.4f, 0.4f));
            EditorGUILayout.Space(5);
        }

        /// <summary>
        /// Draws a Vector3 SerializedProperty field row with optional X/Z disabling.
        /// Writes values directly via FindPropertyRelative to avoid buffered-input issues.
        /// </summary>
        private static void DrawJitterField(SerializedProperty prop, bool disableXZ)
        {
            SerializedProperty px = prop.FindPropertyRelative("x");
            SerializedProperty py = prop.FindPropertyRelative("y");
            SerializedProperty pz = prop.FindPropertyRelative("z");

            Rect r = EditorGUILayout.GetControlRect(true, EditorGUIUtility.singleLineHeight);
            EditorGUI.LabelField(new Rect(r.x, r.y, EditorGUIUtility.labelWidth, r.height),
                new GUIContent(prop.displayName, prop.tooltip));

            float cx     = r.x + EditorGUIUtility.labelWidth;
            float avail  = r.width - EditorGUIUtility.labelWidth;
            float axisW  = 14f;
            float fieldW = Mathf.Max(20f, (avail - axisW * 3f) / 3f);
            GUIStyle al  = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleLeft };

            // X — drag on label, disabled when keepVert
            Rect xLbl = new Rect(cx, r.y, axisW, r.height);
            if (!disableXZ) { EditorGUIUtility.AddCursorRect(xLbl, MouseCursor.SlideArrow); px.floatValue = Mathf.Max(0f, HandleFloatDrag(xLbl, px.floatValue, 0.5f, 0f)); }
            EditorGUI.LabelField(xLbl, "X", al); cx += axisW;
            EditorGUI.BeginDisabledGroup(disableXZ);
            float xv = EditorGUI.FloatField(new Rect(cx, r.y, fieldW, r.height), px.floatValue);
            if (!disableXZ) px.floatValue = Mathf.Max(0f, xv);
            cx += fieldW;
            EditorGUI.EndDisabledGroup();

            // Y — always enabled, drag on label
            Rect yLbl = new Rect(cx, r.y, axisW, r.height);
            EditorGUIUtility.AddCursorRect(yLbl, MouseCursor.SlideArrow);
            py.floatValue = Mathf.Max(0f, HandleFloatDrag(yLbl, py.floatValue, 0.5f, 0f));
            EditorGUI.LabelField(yLbl, "Y", al); cx += axisW;
            py.floatValue = Mathf.Max(0f, EditorGUI.FloatField(new Rect(cx, r.y, fieldW, r.height), py.floatValue)); cx += fieldW;

            // Z — drag on label, disabled when keepVert
            Rect zLbl = new Rect(cx, r.y, axisW, r.height);
            if (!disableXZ) { EditorGUIUtility.AddCursorRect(zLbl, MouseCursor.SlideArrow); pz.floatValue = Mathf.Max(0f, HandleFloatDrag(zLbl, pz.floatValue, 0.5f, 0f)); }
            EditorGUI.LabelField(zLbl, "Z", al); cx += axisW;
            EditorGUI.BeginDisabledGroup(disableXZ);
            float zv = EditorGUI.FloatField(new Rect(cx, r.y, fieldW, r.height), pz.floatValue);
            if (!disableXZ) pz.floatValue = Mathf.Max(0f, zv);
            EditorGUI.EndDisabledGroup();
        }

        private void DrawAssetSection(EasyFenceTool script, string title, bool isPost)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            
            int currentSourceMode = isPost ? script.PostSourceMode : script.FenceSourceMode;
            List<GameObject> currentPrefabsList = isPost ? script.PostPrefabs : script.FencePrefabs;
            List<GameObject> currentExcludedList = isPost ? script.ExcludedPostPrefabs : script.ExcludedFencePrefabs;
            
            GUILayout.BeginHorizontal();
            Color defaultBg = GUI.backgroundColor;
            GUI.backgroundColor = currentSourceMode == 0 ? new Color(0.4f, 0.9f, 0.4f) : defaultBg;
            if (GUILayout.Button(new GUIContent("Manual List", "Pick prefabs individually. Left-click a thumbnail to include/exclude it from generation."), EditorStyles.miniButtonLeft, GUILayout.Height(24))) SetSourceMode(0, isPost);
            GUI.backgroundColor = currentSourceMode == 1 ? new Color(0.4f, 0.9f, 0.4f) : defaultBg;
            if (GUILayout.Button(new GUIContent("Folder Directory", "Automatically load all prefabs found inside a dropped folder. Useful for large prefab sets."), EditorStyles.miniButtonRight, GUILayout.Height(24))) SetSourceMode(1, isPost);
            GUI.backgroundColor = defaultBg;
            GUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            Rect dropRect = GUILayoutUtility.GetRect(0f, 44f, GUILayout.ExpandWidth(true));
            if (_dropStyle == null) _dropStyle = new GUIStyle(EditorStyles.helpBox) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, fontSize = 13 };
            string dropText;
            if (currentSourceMode == 0)
            {
                dropText = "Drag & Drop Prefabs / Folder Here";
            }
            else
            {
                var assignedFolder = isPost ? script.PostFolder : script.FenceFolder;
                dropText = assignedFolder != null ? "📁  " + assignedFolder.name : "Drag & Drop Folder Here";
            }
            GUI.Box(dropRect, dropText, _dropStyle);

            Event evt = Event.current;
            if (evt.type == EventType.DragUpdated || evt.type == EventType.DragPerform)
            {
                if (dropRect.Contains(evt.mousePosition))
                {
                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                    if (evt.type == EventType.DragPerform)
                    {
                        DragAndDrop.AcceptDrag();
                        foreach (Object draggedObj in DragAndDrop.objectReferences)
                        {
                            if (draggedObj is DefaultAsset folder)
                            {
                                foreach (EasyFenceTool t in targets)
                                {
                                    Undo.RecordObject(t, "Assign Folder");
                                    if (isPost) { t.PostSourceMode = 1; t.PostFolder = folder; t.ExcludedPostPrefabs.Clear(); }
                                    else { t.FenceSourceMode = 1; t.FenceFolder = folder; t.ExcludedFencePrefabs.Clear(); }
                                    t.TriggerRebuild();
                                    EditorUtility.SetDirty(t);
                                }
                            }
                            else if (draggedObj is GameObject go)
                            {
                                foreach (EasyFenceTool t in targets)
                                {
                                    Undo.RecordObject(t, "Add Prefab");
                                    if (isPost)
                                    {
                                        t.PostSourceMode = 0;
                                        if (!t.PostPrefabs.Contains(go)) t.PostPrefabs.Add(go);
                                    }
                                    else
                                    {
                                        t.FenceSourceMode = 0;
                                        if (!t.FencePrefabs.Contains(go))
                                        {
                                            t.FencePrefabs.Add(go);
                                        }
                                    }
                                    t.TriggerRebuild();
                                    EditorUtility.SetDirty(t);
                                }
                            }
                        }
                        evt.Use();
                    }
                }
            }

            GUILayout.BeginHorizontal();
            if (currentSourceMode == 0 && currentPrefabsList.Count > 0)
            {
                if (GUILayout.Button(new GUIContent("Clear List", "Remove all prefabs from the manual list and reset their weights."), EditorStyles.miniButton))
                {
                    foreach (EasyFenceTool t in targets)
                    {
                        Undo.RecordObject(t, "Clear Prefabs");
                        if (isPost) { t.PostPrefabs.Clear(); t.ExcludedPostPrefabs.Clear(); t.PostPrefabWeightIds.Clear(); t.PostPrefabWeightValues.Clear(); }
                        else { t.FencePrefabs.Clear(); t.ExcludedFencePrefabs.Clear(); t.FencePrefabWeightIds.Clear(); t.FencePrefabWeightValues.Clear(); }
                        t.TriggerRebuild();
                        EditorUtility.SetDirty(t);
                    }
                }
            }
            if (currentExcludedList.Count > 0)
            {
                if (GUILayout.Button(new GUIContent("Enable All", "Re-include all excluded prefabs so every asset in the list participates in generation."), EditorStyles.miniButton))
                {
                    foreach (EasyFenceTool t in targets)
                    {
                        Undo.RecordObject(t, "Restore Excluded");
                        if (isPost) t.ExcludedPostPrefabs.Clear();
                        else t.ExcludedFencePrefabs.Clear();
                        t.TriggerRebuild();
                        EditorUtility.SetDirty(t);
                    }
                }
            }
            GUILayout.EndHorizontal();

            var allPrefabs = isPost ? script.GetAllSourcePostPrefabs() : script.GetAllSourceFencePrefabs();
            if (allPrefabs.Count > 0)
            {
                EditorGUILayout.Space(5);

                GUILayout.BeginHorizontal();
                if (GUILayout.Button(new GUIContent("All 100%", "Reset all prefab weights to 100% — equal probability for every asset."), EditorStyles.miniButton))
                {
                    foreach (EasyFenceTool s in targets)
                    {
                        Undo.RecordObject(s, "Set All Weights 100%");
                        List<string> ids  = isPost ? s.PostPrefabWeightIds  : s.FencePrefabWeightIds;
                        List<float>  vals = isPost ? s.PostPrefabWeightValues : s.FencePrefabWeightValues;
                        ids.Clear();
                        vals.Clear();
                        s.TriggerRebuild();
                        EditorUtility.SetDirty(s);
                    }
                }
                if (GUILayout.Button(new GUIContent("All 0% *", "Set all weights to 0% except the first prefab which keeps its current value. Useful for isolating one asset."), EditorStyles.miniButton))
                {
                    foreach (EasyFenceTool s in targets)
                    {
                        Undo.RecordObject(s, "Set All Weights 0%");
                        List<string> ids  = isPost ? s.PostPrefabWeightIds  : s.FencePrefabWeightIds;
                        List<float>  vals = isPost ? s.PostPrefabWeightValues : s.FencePrefabWeightValues;
                        float firstWeight = allPrefabs.Count > 0
                            ? (isPost ? s.GetPostPrefabWeight(allPrefabs[0]) : s.GetFencePrefabWeight(allPrefabs[0]))
                            : 100f;
                        ids.Clear();
                        vals.Clear();
                        for (int pi = 0; pi < allPrefabs.Count; pi++)
                        {
                            if (allPrefabs[pi] == null) continue;
                            string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(allPrefabs[pi]));
                            ids.Add(guid);
                            vals.Add(pi == 0 ? firstWeight : 0f);
                        }
                        s.TriggerRebuild();
                        EditorUtility.SetDirty(s);
                    }
                }
                GUILayout.EndHorizontal();
                EditorGUILayout.Space(3);

                int columns = Mathf.Max(1, (int)(EditorGUIUtility.currentViewWidth / 72));
                int col = 0;
                GUILayout.BeginHorizontal();
                
                if (_weightLabelStyle == null)
                    _weightLabelStyle = new GUIStyle(EditorStyles.miniLabel)
                        { normal = { textColor = Color.white }, fontStyle = FontStyle.Bold, alignment = TextAnchor.LowerLeft };

                foreach (var p in allPrefabs)
                {
                    bool isExcluded = currentExcludedList.Contains(p);
                    int  weightInt  = isExcluded ? 100 : Mathf.RoundToInt(
                        isPost ? script.GetPostPrefabWeight(p) : script.GetFencePrefabWeight(p));

                    Texture preview = AssetPreview.GetAssetPreview(p);
                    if (preview == null) preview = AssetPreview.GetMiniThumbnail(p);

                    GUILayout.BeginVertical(GUILayout.Width(64));
                    Rect boxRect = GUILayoutUtility.GetRect(64, 64);

                    // ── 1. DRAG (PRZED przyciskiem — inaczej Button.Use() zjada MouseDrag) ──
                    int newW = weightInt;
                    if (!isExcluded)
                    {
                        newW = HandleWeightDrag(boxRect, weightInt);
                        EditorGUIUtility.AddCursorRect(boxRect, MouseCursor.SlideArrow);
                    }

                    // ── 1b. VERT ICON — handle click BEFORE button ──
                    string pGuidV = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(p));
                    List<string> vertIds = isPost ? script.PostPrefabAlwaysVerticalIds : script.FencePrefabAlwaysVerticalIds;
                    bool isAlwaysVert = vertIds.Contains(pGuidV);
                    Rect vertIconRect = new Rect(boxRect.x + 1, boxRect.y + 1, 16, 16);
                    bool clickedVertIcon = false;
                    if (!isExcluded)
                    {
                        Event eIcon = Event.current;
                        if (eIcon.type == EventType.MouseDown && eIcon.button == 0 && vertIconRect.Contains(eIcon.mousePosition))
                        {
                            clickedVertIcon = true;
                            eIcon.Use();
                        }
                    }

                    // ── 1c. REMOVE ICON (×) — top right corner, Manual List only ──
                    Rect removeIconRect = new Rect(boxRect.xMax - 17, boxRect.y + 1, 16, 16);
                    bool clickedRemove = false;
                    if (currentSourceMode == 0)
                    {
                        Event eRemove = Event.current;
                        if (eRemove.type == EventType.MouseDown && eRemove.button == 0 && removeIconRect.Contains(eRemove.mousePosition))
                        {
                            clickedRemove = true;
                            eRemove.Use();
                        }
                    }

                    // ── 2. BUTTON (click = include/exclude; drag already handled above) ──
                    int mouseBtn = Event.current.button;
                    if (isExcluded) GUI.color = new Color(1f, 1f, 1f, 0.3f);
                    if (GUI.Button(boxRect, new GUIContent(preview, p.name)) && mouseBtn == 0)
                    {
                        foreach (EasyFenceTool s in targets)
                        {
                            Undo.RecordObject(s, isExcluded ? "Include Asset" : "Exclude Asset");
                            List<GameObject> exc = isPost ? s.ExcludedPostPrefabs : s.ExcludedFencePrefabs;
                            if (isExcluded) exc.Remove(p);
                            else if (!exc.Contains(p)) exc.Add(p);
                            s.TriggerRebuild();
                            EditorUtility.SetDirty(s);
                        }
                        GUIUtility.ExitGUI();
                    }
                    GUI.color = Color.white;

                    // ── 3. OVERLAY darkening + icons ──
                    if (!isExcluded)
                    {
                        // ↑ icon in the top left corner
                        EditorGUI.DrawRect(vertIconRect, isAlwaysVert ? new Color(0f, 0.55f, 1f, 0.9f) : new Color(0f, 0f, 0f, 0.45f));
                        GUI.Label(vertIconRect, new GUIContent("↑", "Force this prefab to always stay upright regardless of terrain slope. X/Z rotation jitter is suppressed."),
                            new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = isAlwaysVert ? Color.white : new Color(1f, 1f, 1f, 0.4f) }, alignment = TextAnchor.MiddleCenter, fontSize = 10 });

                        if (clickedVertIcon)
                        {
                            foreach (EasyFenceTool s in targets)
                            {
                                Undo.RecordObject(s, "Toggle Always Vertical");
                                List<string> vIds = isPost ? s.PostPrefabAlwaysVerticalIds : s.FencePrefabAlwaysVerticalIds;
                                if (vIds.Contains(pGuidV)) vIds.Remove(pGuidV);
                                else vIds.Add(pGuidV);
                                s.TriggerRebuild();
                                EditorUtility.SetDirty(s);
                            }
                        }
                    }

                    // × icon in the top right corner (Manual List)
                    if (currentSourceMode == 0)
                    {
                        EditorGUI.DrawRect(removeIconRect, new Color(0.6f, 0f, 0f, 0.75f));
                        GUI.Label(removeIconRect, new GUIContent("×", "Remove this prefab from the list."),
                            new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = Color.white }, alignment = TextAnchor.MiddleCenter, fontSize = 11, fontStyle = FontStyle.Bold });

                        if (clickedRemove)
                        {
                            foreach (EasyFenceTool s in targets)
                            {
                                Undo.RecordObject(s, "Remove Prefab");
                                List<GameObject> lst  = isPost ? s.PostPrefabs  : s.FencePrefabs;
                                List<GameObject> exc  = isPost ? s.ExcludedPostPrefabs : s.ExcludedFencePrefabs;
                                List<string>     wIds = isPost ? s.PostPrefabWeightIds  : s.FencePrefabWeightIds;
                                List<float>      wVls = isPost ? s.PostPrefabWeightValues : s.FencePrefabWeightValues;
                                List<string>     vIds = isPost ? s.PostPrefabAlwaysVerticalIds : s.FencePrefabAlwaysVerticalIds;
                                lst.Remove(p);
                                exc.Remove(p);
                                int wi = wIds.IndexOf(pGuidV);
                                if (wi >= 0) { wIds.RemoveAt(wi); wVls.RemoveAt(wi); }
                                vIds.Remove(pGuidV);
                                s.TriggerRebuild();
                                EditorUtility.SetDirty(s);
                            }
                            GUIUtility.ExitGUI();
                        }
                    }
                    if (!isExcluded)
                    {
                        if (weightInt < 100)
                        {
                            float darkW = (1f - weightInt / 100f) * boxRect.width;
                            EditorGUI.DrawRect(new Rect(boxRect.xMax - darkW, boxRect.y, darkW, boxRect.height),
                                               new Color(0f, 0f, 0f, 0.55f));
                        }
                        GUI.Label(new Rect(boxRect.x + 3, boxRect.yMax - 16, 60, 15),
                                  weightInt + "%", _weightLabelStyle);
                    }

                    // ── 4. SAVE weight if changed ──
                    if (!isExcluded && newW != weightInt)
                    {
                        string pGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(p));
                        foreach (EasyFenceTool s in targets)
                        {
                            Undo.RecordObject(s, "Set Prefab Weight");
                            List<string> ids  = isPost ? s.PostPrefabWeightIds  : s.FencePrefabWeightIds;
                            List<float>  vals = isPost ? s.PostPrefabWeightValues : s.FencePrefabWeightValues;
                            int ti = ids.IndexOf(pGuid);
                            if (ti < 0) { ids.Add(pGuid); vals.Add(newW); }
                            else vals[ti] = newW;
                            s.TriggerRebuild();
                            EditorUtility.SetDirty(s);
                        }
                    }

                    // ── 5. NAME LABEL ──
                    string n = p.name;
                    if (n.Length > 9) n = n.Substring(0, 7) + "..";
                    if (isExcluded) GUI.contentColor = Color.gray;
                    GUILayout.Label(n, EditorStyles.miniLabel, GUILayout.Width(64));
                    GUI.contentColor = Color.white;

                    GUILayout.EndVertical();

                    col++;
                    if (col >= columns)
                    {
                        col = 0;
                        GUILayout.EndHorizontal();
                        GUILayout.BeginHorizontal();
                    }
                }
                GUILayout.EndHorizontal();
            }

            // ════════════════════════════════════════════════════════════════════
            // FENCE SEGMENTS — extra sections
            // ════════════════════════════════════════════════════════════════════
            if (!isPost)
            {
                // ── Natural Variation & Damage ───────────────────────────────
                DrawSectionSeparator();
                EditorGUILayout.LabelField("Natural Variation & Damage", EditorStyles.boldLabel);
                EditorGUILayout.Space(4);

                EditorGUI.BeginChangeCheck();

                EditorGUILayout.PropertyField(serializedObject.FindProperty("MissingSegmentChance"), true);
                EditorGUILayout.Space(3);

                bool keepVert = serializedObject.FindProperty("KeepVertical")?.boolValue == true;
                if (keepVert)
                {
                    EditorGUILayout.HelpBox("Keep Vertical is ON — X and Z jitter are suppressed. Only Y (yaw) applies.", MessageType.None);
                    EditorGUILayout.Space(3);
                }

                // X/Z disabled when keepVert; Y always editable. Uses FindPropertyRelative for reliable value write.
                DrawJitterField(serializedObject.FindProperty("RandomRotationJitter"), keepVert);

                EditorGUILayout.Space(3);
                SerializedProperty scaleProp = serializedObject.FindProperty("RandomScaleRange");
                EditorGUILayout.PropertyField(scaleProp, true);
                Vector2 sv = scaleProp.vector2Value;
                sv.x = Mathf.Max(0f, sv.x);
                sv.y = Mathf.Max(sv.x, sv.y);
                scaleProp.vector2Value = sv;

                if (EditorGUI.EndChangeCheck())
                {
                    foreach (EasyFenceTool t in targets) t.TriggerRebuild();
                    SceneView.RepaintAll();
                }

                EditorGUILayout.Space(5);
                if (GUILayout.Button(new GUIContent("🔀 Randomize Variations", "Re-rolls the random seed to generate a new variation pattern for scale, rotation and missing segments."), GUILayout.Height(28)))
                {
                    foreach (EasyFenceTool s in targets)
                    {
                        Undo.RecordObject(s, "Randomize Fence");
                        s.RandomSeed = UnityEngine.Random.Range(1, int.MaxValue);
                        s.TriggerRebuild();
                        EditorUtility.SetDirty(s);
                    }
                    SceneView.RepaintAll();
                }

                // ── Placement & Geometry ─────────────────────────────────────
                DrawSectionSeparator();
                EditorGUILayout.LabelField("Placement & Geometry", EditorStyles.boldLabel);
                EditorGUILayout.Space(4);

                EditorGUI.BeginChangeCheck();

                EditorGUILayout.PropertyField(serializedObject.FindProperty("AutoCalculateLength"), true);
                bool autoLen = serializedObject.FindProperty("AutoCalculateLength")?.boolValue == true;
                float minLen = script.GetPhysicalMinLength();
                SerializedProperty segLenProp = serializedObject.FindProperty("SegmentLength");
                EditorGUI.BeginDisabledGroup(autoLen);
                EditorGUILayout.PropertyField(segLenProp, new GUIContent($"Segment Length (Min: {minLen:F2})", "Used only when Auto Calculate Length is OFF. Cannot be set below the physical mesh size."));
                if (!autoLen && segLenProp.floatValue < minLen) segLenProp.floatValue = minLen;
                EditorGUI.EndDisabledGroup();
                EditorGUILayout.Space(2);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("RotationOffset"), true);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("PositionOffset"), true);
                {
                    SerializedProperty omProp = serializedObject.FindProperty("FenceOffsetMode");
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(new GUIContent("Offset Mode",
                        "Standard — Position Offset axes follow the path (X=across, Y=up, Z=along). Predictable regardless of rotation.\n\n" +
                        "Freeform ✦ — Offset is applied in the segment's local rotated space (legacy behaviour)."),
                        GUILayout.Width(EditorGUIUtility.labelWidth));
                    if (GUILayout.Toggle(omProp.enumValueIndex == 0, "Standard",       EditorStyles.miniButtonLeft))  omProp.enumValueIndex = 0;
                    if (GUILayout.Toggle(omProp.enumValueIndex == 1, "Freeform  ✦", EditorStyles.miniButtonRight)) omProp.enumValueIndex = 1;
                    EditorGUILayout.EndHorizontal();
                }

                if (EditorGUI.EndChangeCheck())
                {
                    foreach (EasyFenceTool t in targets) t.TriggerRebuild();
                    SceneView.RepaintAll();
                }
            }

            // ════════════════════════════════════════════════════════════════════
            // POSTS & CORNERS — extra sections
            // ════════════════════════════════════════════════════════════════════
            if (isPost)
            {
                // ── Posts & Corners Settings ─────────────────────────────────
                DrawSectionSeparator();
                EditorGUILayout.LabelField("Posts & Corners Settings", EditorStyles.boldLabel);
                EditorGUILayout.Space(4);

                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(serializedObject.FindProperty("PlacementMode"), true);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("CornerAngleThreshold"), true);
                EditorGUILayout.Space(2);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("PostVerticalOffset"), true);
                if (EditorGUI.EndChangeCheck())
                {
                    foreach (EasyFenceTool t in targets) t.TriggerRebuild();
                    SceneView.RepaintAll();
                }

                // ── Natural Variation & Damage ───────────────────────────────
                DrawSectionSeparator();
                EditorGUILayout.LabelField("Natural Variation & Damage", EditorStyles.boldLabel);
                EditorGUILayout.Space(4);

                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(serializedObject.FindProperty("PostMissingChance"), true);
                EditorGUILayout.Space(3);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("PostRandomRotationJitter"), true);
                EditorGUILayout.Space(3);
                SerializedProperty postScaleProp = serializedObject.FindProperty("PostRandomScaleRange");
                EditorGUILayout.PropertyField(postScaleProp, true);
                Vector2 psv = postScaleProp.vector2Value;
                psv.x = Mathf.Max(0f, psv.x);
                psv.y = Mathf.Max(psv.x, psv.y);
                postScaleProp.vector2Value = psv;
                if (EditorGUI.EndChangeCheck())
                {
                    foreach (EasyFenceTool t in targets) t.TriggerRebuild();
                    SceneView.RepaintAll();
                }

                EditorGUILayout.Space(5);
                if (GUILayout.Button(new GUIContent("🔀 Randomize Posts", "Re-rolls the post random seed independently from fence segments."), GUILayout.Height(28)))
                {
                    foreach (EasyFenceTool s in targets)
                    {
                        Undo.RecordObject(s, "Randomize Posts");
                        s.PostRandomSeed = UnityEngine.Random.Range(1, int.MaxValue);
                        s.TriggerRebuild();
                        EditorUtility.SetDirty(s);
                    }
                    SceneView.RepaintAll();
                }

                // ── Placement & Geometry ─────────────────────────────────────
                DrawSectionSeparator();
                EditorGUILayout.LabelField("Placement & Geometry", EditorStyles.boldLabel);
                EditorGUILayout.Space(4);

                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(serializedObject.FindProperty("PostRotationOffset"), true);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("PostPositionOffset"), true);
                {
                    SerializedProperty pomProp = serializedObject.FindProperty("PostOffsetMode");
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(new GUIContent("Offset Mode",
                        "Standard — Post Position Offset axes follow the path (X=across, Y=up, Z=along). Bisects corner direction at multi-point junctions.\n\n" +
                        "Freeform ✦ — Offset is applied in the post's local rotated space (legacy behaviour)."),
                        GUILayout.Width(EditorGUIUtility.labelWidth));
                    if (GUILayout.Toggle(pomProp.enumValueIndex == 0, "Standard",       EditorStyles.miniButtonLeft))  pomProp.enumValueIndex = 0;
                    if (GUILayout.Toggle(pomProp.enumValueIndex == 1, "Freeform  ✦", EditorStyles.miniButtonRight)) pomProp.enumValueIndex = 1;
                    EditorGUILayout.EndHorizontal();
                }
                if (EditorGUI.EndChangeCheck())
                {
                    foreach (EasyFenceTool t in targets) t.TriggerRebuild();
                    SceneView.RepaintAll();
                }
            }

            EditorGUILayout.EndVertical();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var script = (EasyFenceTool)target;

            if (_headerImage != null)
            {
                Rect logoRect = GUILayoutUtility.GetRect(0f, 250f, GUILayout.ExpandWidth(true));
                GUI.DrawTexture(logoRect, _headerImage, ScaleMode.ScaleAndCrop);
                EditorGUILayout.Space(5);
            }

            EditorGUILayout.Space(10);

            // Pulsate when empty — draws the eye even when user is looking at Inspector
            bool _noPoints = script.Points.Count == 0;
            if (_noPoints)
            {
                float _pulse = Mathf.Abs(Mathf.Sin((float)EditorApplication.timeSinceStartup * 2.2f));
                GUI.backgroundColor = Color.Lerp(new Color(0.25f, 0.65f, 1f), Color.white, _pulse * 0.55f);
                Repaint(); // keep inspector animating while empty
            }
            else
            {
                GUI.backgroundColor = new Color(0.4f, 0.8f, 1f, 1f);
            }
            if (GUILayout.Button(new GUIContent("⦿  ADD INITIAL POINT  ⦿",
                _noPoints
                    ? "Path is empty — click this button OR click directly in the Scene View to place the first point."
                    : "Places an additional start point at the scene view centre."),
                GUILayout.Height(42)))
            {
                foreach (EasyFenceTool s in targets)
                {
                    if (s.Points.Count == 0)
                    {
                        Undo.RecordObject(s, "Add Initial Point");
                        s.Points.Add(GetSceneViewGroundPosition(s));
                        s.TriggerRebuild();
                        EditorUtility.SetDirty(s);
                    }
                }
                SceneView.RepaintAll();
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(8);

            // ── Quick Shapes ─────────────────────────────────────────────────
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Quick Shapes", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            int[]  _ptsSteps = { 8, 16, 32, 64 };
            // Use the LONGEST prefab for constraints — guarantees all panels fit on every segment
            float  _minLen   = script.GetPhysicalMaxLength();
            float  _postR    = script.GetMaxPostRadius();
            float  _sizeMin  = script.ShapeIsCircle ? 10f : 5f;

            // ── Size — label drag + numeric field ───────────────────────────
            EditorGUI.BeginChangeCheck();
            float newSize = Mathf.Max(_sizeMin, script.ShapeSize);
            {
                Rect sr   = EditorGUILayout.GetControlRect(true, EditorGUIUtility.singleLineHeight);
                Rect sLbl = new Rect(sr.x, sr.y, EditorGUIUtility.labelWidth, sr.height);
                Rect sFld = new Rect(sr.x + EditorGUIUtility.labelWidth, sr.y, sr.width - EditorGUIUtility.labelWidth, sr.height);
                EditorGUI.LabelField(sLbl, new GUIContent("Size", "Radius (Circle) or side length (Square). Circle minimum: 10. Drag label to adjust."));
                EditorGUIUtility.AddCursorRect(sLbl, MouseCursor.SlideArrow);
                newSize = Mathf.Max(_sizeMin, HandleFloatDrag(sLbl, newSize, 0.1f, _sizeMin));
                newSize = Mathf.Max(_sizeMin, EditorGUI.FloatField(sFld, newSize));
            }
            // Size must stay large enough for current pts
            float _minR = MinRadiusForPts(script.ShapeResolution, _minLen, _postR);
            if (newSize < _minR) newSize = _minR;
            bool sizeChanged = EditorGUI.EndChangeCheck();

            if (sizeChanged && newSize != script.ShapeSize)
            {
                Undo.RecordObject(script, "Shape Size");
                script.ShapeSize   = newSize;
                script.ShapeActive = true;
                EditorUtility.SetDirty(script);
                RegenerateAndRebuild(script);
                SceneView.RepaintAll();
            }

            // ── Pts — dropdown with grayed-out invalid values ────────────────
            {
                int _validMax = MaxPtsForRadius(script.ShapeSize, _minLen, _postR, _ptsSteps);
                Rect pr   = EditorGUILayout.GetControlRect(true, EditorGUIUtility.singleLineHeight);
                Rect pLbl = new Rect(pr.x, pr.y, EditorGUIUtility.labelWidth, pr.height);
                Rect pBtn = new Rect(pr.x + EditorGUIUtility.labelWidth, pr.y, pr.width - EditorGUIUtility.labelWidth, pr.height);
                EditorGUI.LabelField(pLbl, new GUIContent("Pts",
                    "Circle path points: 8 / 16 / 32 / 64. Options grayed out if Size is too small to fit a fence at that resolution."));
                if (EditorGUI.DropdownButton(pBtn, new GUIContent($"{script.ShapeResolution} pts"), FocusType.Keyboard))
                {
                    GenericMenu menu = new GenericMenu();
                    EasyFenceTool capt      = script;
                    int           captValid = _validMax;
                    foreach (int step in _ptsSteps)
                    {
                        int captStep = step;
                        if (step <= captValid)
                        {
                            menu.AddItem(new GUIContent($"{step} pts"), step == capt.ShapeResolution, () =>
                            {
                                Undo.RecordObject(capt, "Shape Pts");
                                capt.ShapeResolution = captStep;
                                capt.ShapeActive     = true;
                                EditorUtility.SetDirty(capt);
                                RegenerateAndRebuild(capt);
                                SceneView.RepaintAll();
                            });
                        }
                        else
                        {
                            // Grayed out — user must increase Size first
                            menu.AddDisabledItem(new GUIContent($"{step} pts  (increase Size first)"), false);
                        }
                    }
                    menu.ShowAsContext();
                }
            }

            EditorGUILayout.Space(5);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(new GUIContent("⬤  Circle", "Generates a closed circular path using current Size (radius) and Pts. Smart constraints ensure fence segments always fit."), GUILayout.Height(28)))
                GenerateShape(script, true);
            if (GUILayout.Button(new GUIContent("■  Square", "Generates a closed square path using current Size as side length."), GUILayout.Height(28)))
                GenerateShape(script, false);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
            // ────────────────────────────────────────────────────────────────

            EditorGUILayout.Space(8);

            GUI.backgroundColor = _isEditing ? new Color(0.6f, 1f, 0.6f) : Color.white;
            if (GUILayout.Button(new GUIContent(_isEditing ? "✎  EDITING MODE ACTIVE  ✎" : "▶  ENABLE EDITING MODE  ◀", "Toggle path editing. When active: Shift+Click adds a point, Ctrl+Click removes, drag handles move points."), GUILayout.Height(36)))
            {
                _isEditing = !_isEditing;
                Tools.hidden = _isEditing;
                SceneView.RepaintAll();
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.HelpBox(
                "Shortcuts:\n" +
                "Shift + Click    → Add/Insert point\n" +
                "Ctrl + Click     → Remove point\n" +
                "Drag Handle      → Move point\n\n" +
                "Enable 'Gizmos' in Scene View to see handles.",
                MessageType.Info);

            EditorGUILayout.Space(10);

            DrawAssetSection(script, "Fence Segments", false);
            EditorGUILayout.Space(10);
            DrawAssetSection(script, "Posts & Corners", true);

            EditorGUILayout.Space(4);
            EditorGUI.BeginChangeCheck();
            
            SerializedProperty prop = serializedObject.GetIterator();
            bool enterChildren = true;
            while (prop.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (prop.name == "m_Script") continue;
                
                // All these are drawn inside DrawAssetSection modules — skip them here.
                if (prop.name == "MissingSegmentChance"    || prop.name == "RandomRotationJitter"     || prop.name == "RandomScaleRange")    continue;
                if (prop.name == "PlacementMode"           || prop.name == "CornerAngleThreshold"     || prop.name == "PostVerticalOffset")  continue;
                if (prop.name == "AutoCalculateLength"     || prop.name == "SegmentLength"            || prop.name == "RotationOffset"       || prop.name == "PositionOffset") continue;
                if (prop.name == "PostMissingChance"       || prop.name == "PostRandomRotationJitter" || prop.name == "PostRandomScaleRange") continue;
                if (prop.name == "PostRotationOffset"      || prop.name == "PostPositionOffset")      continue;

                EditorGUILayout.PropertyField(prop, true);
            }

            if (EditorGUI.EndChangeCheck())
            {
                foreach (var t in targets) (t as EasyFenceTool)?.TriggerRebuild();
                SceneView.RepaintAll();
            }

            EditorGUILayout.Space(12);

            var activeFence = script.GetActiveFencePrefabs();
            var activePosts = script.GetActivePostPrefabs();
            
            if (activeFence.Count == 0 && activePosts.Count == 0)
                EditorGUILayout.HelpBox("No active prefabs found. Preview and Bake won't work.", MessageType.Warning);
            else if (activeFence.Count == 0)
                EditorGUILayout.HelpBox("No active fence segments found. Only posts will be generated.", MessageType.Info);

            EditorGUILayout.Space(10);

            using (new EditorGUI.DisabledGroupScope(script.Points.Count < 1))
            {
                GUI.backgroundColor = new Color(0.7f, 1f, 0.7f);
                if (GUILayout.Button(new GUIContent("✔  BAKE FENCE  ✔", "Instantiate all fence segments and posts as real prefabs in the scene. The preview objects are replaced with actual GameObjects."), GUILayout.Height(36))) Bake();
                GUI.backgroundColor = Color.white;
            }

            if (GUILayout.Button(new GUIContent("🗑  Clear Path", "Delete all path points and reset the fence layout. Cannot be undone."), GUILayout.Height(24)))
            {
                if (EditorUtility.DisplayDialog("Clear Path?", "Are you sure you want to delete all points?", "Yes", "No"))
                {
                    foreach (EasyFenceTool s in targets)
                    {
                        Undo.RecordObject(s, "Clear Points");
                        s.Points.Clear();
                        s.TriggerRebuild();
                        EditorUtility.SetDirty(s);
                    }
                    SceneView.RepaintAll();
                }
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void SetSourceMode(int mode, bool isPost)
        {
            foreach (EasyFenceTool t in targets)
            {
                Undo.RecordObject(t, "Change Source Mode");
                if (isPost) t.PostSourceMode = mode;
                else t.FenceSourceMode = mode;
                t.TriggerRebuild();
                EditorUtility.SetDirty(t);
            }
        }

        private Vector3 GetSceneViewGroundPosition(EasyFenceTool script)
        {
            if (SceneView.lastActiveSceneView?.camera != null)
            {
                Camera cam = SceneView.lastActiveSceneView.camera;
                // Ray through screen centre (viewport 0.5, 0.5) — places first point in view centre
                Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

                RaycastHit[] hits = Physics.RaycastAll(ray, 10000f, script.TerrainMask, QueryTriggerInteraction.Ignore);
                
                float bestDist = float.MaxValue;
                bool found = false;
                Vector3 hitPoint = Vector3.zero;

                foreach (var hit in hits)
                {
                    if (script.SnapOnlyToUnityTerrain && !(hit.collider is TerrainCollider)) continue;
                    
                    if (hit.distance < bestDist)
                    {
                        bestDist = hit.distance;
                        hitPoint = hit.point;
                        found = true;
                    }
                }

                if (found) return hitPoint;

                float fallbackY = script.Points.Count > 0 ? script.Points[script.Points.Count - 1].y : 0f;
                Plane plane = new Plane(Vector3.up, new Vector3(0, fallbackY, 0));
                if (plane.Raycast(ray, out float dist)) return ray.GetPoint(dist);
            }
            return script.transform.position;
        }

        private void OnSceneGUI()
        {
            var script = (EasyFenceTool)target;
            Event e = Event.current;

            // ── Scene view info overlay ──────────────────────────────────────────
            int overlayPts   = script.Points.Count;
            int overlaySegs  = overlayPts < 2 ? 0 : (script.IsLoop ? overlayPts : overlayPts - 1);
            int overlayPosts = 0;
            for (int i = 0; i < overlayPts; i++)
                if (script.ShouldPlacePost(i, overlayPts, script.IsLoop)) overlayPosts++;

            Handles.BeginGUI();
            GUIStyle overlayBox = new GUIStyle(EditorStyles.helpBox)
            {
                fontSize = 11,
                padding  = new RectOffset(8, 8, 5, 5)
            };
            GUIStyle overlayLabel = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 11,
                normal   = { textColor = Color.white }
            };
            float _svH = SceneView.currentDrawingSceneView != null ? SceneView.currentDrawingSceneView.camera.pixelHeight : 400f;
            float _svW = SceneView.currentDrawingSceneView != null ? SceneView.currentDrawingSceneView.camera.pixelWidth  : 800f;

            if (overlayPts == 0 && _isEditing)
            {
                // Large animated "Click to place first point" hint in the centre of the scene
                float _p2 = Mathf.Abs(Mathf.Sin((float)EditorApplication.timeSinceStartup * 2f));
                GUIStyle startHint = new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize  = 14,
                    alignment = TextAnchor.MiddleCenter,
                    normal    = { textColor = new Color(0f, 0.9f, 1f, Mathf.Lerp(0.55f, 1f, _p2)) }
                };
                GUI.Label(new Rect(_svW * 0.5f - 220, _svH * 0.5f - 18, 440, 36),
                    "▶  Click in Scene to place first point  ◀", startHint);
                SceneView.currentDrawingSceneView?.Repaint();
            }
            else
            {
                Rect overlayArea = new Rect(8, _svH - 48f, 260, 26);
                GUI.Box(overlayArea, GUIContent.none, overlayBox);
                GUI.Label(new Rect(overlayArea.x + 8, overlayArea.y + 4, overlayArea.width - 16, overlayArea.height),
                    $"pts {overlayPts}   segs {overlaySegs}   posts {overlayPosts}", overlayLabel);
            }
            Handles.EndGUI();
            // ────────────────────────────────────────────────────────────────────

            // Scale tool → absorb into ShapeSize and live-regenerate
            if (script.ShapeActive && script.transform.localScale != Vector3.one)
            {
                float sxz = (script.transform.localScale.x + script.transform.localScale.z) * 0.5f;
                if (Mathf.Abs(sxz - 1f) > 0.001f)
                {
                    int[]  _steps = { 8, 16, 32, 64 };
                    float  _mLen  = script.GetPhysicalMaxLength();
                    float  _pR    = script.GetMaxPostRadius();
                    Undo.RecordObject(script, "Scale Shape");
                    float shapeMin = script.ShapeIsCircle ? 10f : 5f;
                    script.ShapeSize = Mathf.Max(shapeMin, script.ShapeSize * sxz);
                    int maxPts = MaxPtsForRadius(script.ShapeSize, _mLen, _pR, _steps);
                    if (script.ShapeResolution > maxPts) script.ShapeResolution = maxPts;
                }
                script.transform.localScale = Vector3.one;
                RegenerateAndRebuild(script);
                EditorUtility.SetDirty(script);
            }

            if (_isEditing) HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));

            // ── Ghost point — visible when path is empty, click places first point ───────────────
            if (_isEditing && script.Points.Count == 0)
            {
                // Update ghost position every event (terrain-snapped, same logic as Shift+Click)
                Ray ghostRay = HandleUtility.GUIPointToWorldRay(e.mousePosition);
                RaycastHit[] ghits = Physics.RaycastAll(ghostRay, 10000f, script.TerrainMask, QueryTriggerInteraction.Ignore);
                _ghostValid = false;
                float _gBestD = float.MaxValue;
                foreach (var gh in ghits)
                {
                    if (script.SnapOnlyToUnityTerrain && !(gh.collider is TerrainCollider)) continue;
                    if (gh.distance < _gBestD) { _gBestD = gh.distance; _ghostPos = gh.point; _ghostValid = true; }
                }
                if (!_ghostValid)
                {
                    Plane _gPlane = new Plane(Vector3.up, Vector3.zero);
                    if (_gPlane.Raycast(ghostRay, out float _gDist)) { _ghostPos = ghostRay.GetPoint(_gDist); _ghostValid = true; }
                }

                // Draw pulsating ghost sphere during Repaint
                if (_ghostValid && e.type == EventType.Repaint)
                {
                    float _gPulse = Mathf.Abs(Mathf.Sin((float)EditorApplication.timeSinceStartup * 3f));
                    Handles.color = new Color(0f, 0.9f, 1f, Mathf.Lerp(0.2f, 0.65f, _gPulse));
                    Handles.SphereHandleCap(0, _ghostPos, Quaternion.identity,
                        HandleUtility.GetHandleSize(_ghostPos) * 0.18f, EventType.Repaint);
                    Handles.color = Color.white;
                    SceneView.RepaintAll(); // keep pulsating
                }

                // Regular click (no modifier) places first point — no Shift required
                if (_ghostValid && e.type == EventType.MouseDown && e.button == 0 && !e.control && !e.shift && !e.alt)
                {
                    Undo.RecordObject(script, "Add First Point");
                    script.Points.Add(_ghostPos);
                    script.ShapeActive = false;
                    script.TriggerRebuild();
                    EditorUtility.SetDirty(script);
                    e.Use();
                }
            }
            // ────────────────────────────────────────────────────────────────────────────────────

            if (_isEditing && e.shift && !e.control && e.type == EventType.MouseDown && e.button == 0)
            {
                // ── Step 1: find closest segment in SCREEN SPACE (camera-angle independent) ──
                int segCount = script.IsLoop ? script.Points.Count : script.Points.Count - 1;
                float minScreenDist = float.MaxValue;
                int closestIdx = -1;
                for (int i = 0; i < segCount; i++)
                {
                    Vector3 sp1 = script.SnapToTerrain ? script.GetGroundPos(script.Points[i]) : script.Points[i];
                    Vector3 sp2 = script.SnapToTerrain ? script.GetGroundPos(script.Points[(i + 1) % script.Points.Count]) : script.Points[(i + 1) % script.Points.Count];
                    float sd = HandleUtility.DistanceToLine(sp1, sp2);
                    if (sd < minScreenDist) { minScreenDist = sd; closestIdx = i; }
                }

                // ── Step 2: get 3D hit position via terrain raycast ──
                Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
                RaycastHit[] hits = Physics.RaycastAll(ray, 10000f, script.TerrainMask, QueryTriggerInteraction.Ignore);
                float bestDist = float.MaxValue;
                bool found = false;
                Vector3 pos = Vector3.zero;

                foreach (var h in hits)
                {
                    if (script.SnapOnlyToUnityTerrain && !(h.collider is TerrainCollider)) continue;
                    if (h.distance < bestDist) { bestDist = h.distance; pos = h.point; found = true; }
                }

                // Fallback: flat plane at last point's height
                if (!found)
                {
                    float fallY = script.Points.Count > 0 ? script.Points[script.Points.Count - 1].y : 0f;
                    Plane plane = new Plane(Vector3.up, new Vector3(0f, fallY, 0f));
                    if (plane.Raycast(ray, out float pDist)) { pos = ray.GetPoint(pDist); found = true; }
                }

                // Last fallback: midpoint of closest screen-space segment (handles nearly-horizontal rays)
                if (!found && closestIdx != -1)
                {
                    Vector3 fp1 = script.SnapToTerrain ? script.GetGroundPos(script.Points[closestIdx]) : script.Points[closestIdx];
                    Vector3 fp2 = script.SnapToTerrain ? script.GetGroundPos(script.Points[(closestIdx + 1) % script.Points.Count]) : script.Points[(closestIdx + 1) % script.Points.Count];
                    pos = (fp1 + fp2) * 0.5f;
                    found = true;
                }

                if (found)
                {
                    Undo.RecordObject(script, "Add Point");

                    const float kScreenInsertPx = 30f;
                    if (closestIdx != -1 && minScreenDist < kScreenInsertPx)
                        script.Points.Insert(closestIdx + 1, pos);
                    else
                        script.Points.Add(pos);

                    script.ShapeActive = false;
                    script.TriggerRebuild();
                    EditorUtility.SetDirty(script);
                    e.Use();
                }
            }

            if (_isEditing && e.control && !e.shift && e.type == EventType.MouseDown && e.button == 0)
            {
                float minDist = float.MaxValue;
                int idxToRemove = -1;
                for (int i = 0; i < script.Points.Count; i++)
                {
                    Vector3 checkPos = script.SnapToTerrain ? script.GetGroundPos(script.Points[i]) : script.Points[i];
                    float d = HandleUtility.DistanceToCircle(checkPos, 0.5f);
                    if (d < minDist) { minDist = d; idxToRemove = i; }
                }

                if (idxToRemove != -1 && minDist < 30f)
                {
                    Undo.RecordObject(script, "Remove Point");
                    script.Points.RemoveAt(idxToRemove);
                    script.ShapeActive = false;
                    script.TriggerRebuild();
                    EditorUtility.SetDirty(script);
                    e.Use();
                }
            }

            // ── Collision/overlap highlight — only during Repaint, no extra graphics ────────────
            if (e.type == EventType.Repaint && script.Points.Count >= 2)
            {
                float hlMinLen = script.GetPhysicalMaxLength(); // warn when largest panel can't fit
                float hlPostR  = script.GetMaxPostRadius();
                float minChord = hlMinLen + hlPostR * 2f;
                int   hlSegs   = script.IsLoop ? script.Points.Count : script.Points.Count - 1;
                if (minChord > 0f)
                {
                    Color prevHandleColor = Handles.color;
                    for (int i = 0; i < hlSegs; i++)
                    {
                        Vector3 hp1 = script.SnapToTerrain ? script.GetGroundPos(script.Points[i])                              : script.Points[i];
                        Vector3 hp2 = script.SnapToTerrain ? script.GetGroundPos(script.Points[(i + 1) % script.Points.Count]) : script.Points[(i + 1) % script.Points.Count];
                        if (Vector3.Distance(hp1, hp2) < minChord)
                        {
                            // Thick semi-transparent red anti-aliased line — no extra markers
                            Handles.color = new Color(1f, 0.08f, 0.08f, 0.80f);
                            Handles.DrawAAPolyLine(8f, hp1, hp2);
                        }
                    }
                    Handles.color = prevHandleColor;
                }
            }
            // ────────────────────────────────────────────────────────────────────────────────────

            // Release the locked handle on mouse up
            if (e.type == EventType.MouseUp) _activeHandleIdx = -1;

            // Exactly ONE PositionHandle is visible at a time: whichever point is closest to
            // the mouse cursor. Once a drag starts, that point stays locked (_activeHandleIdx)
            // until mouse up regardless of cursor movement.
            int _nearestPtIdx = -1;
            if (_isEditing && !e.shift && !e.control && script.Points.Count > 0)
            {
                float _nearestDist = float.MaxValue;
                for (int i = 0; i < script.Points.Count; i++)
                {
                    Vector3 wp = script.SnapToTerrain ? script.GetGroundPos(script.Points[i]) : script.Points[i];
                    float   d  = Vector2.Distance(HandleUtility.WorldToGUIPoint(wp), e.mousePosition);
                    if (d < _nearestDist) { _nearestDist = d; _nearestPtIdx = i; }
                }
            }

            for (int i = 0; i < script.Points.Count; i++)
            {
                Vector3 currentPos = script.SnapToTerrain ? script.GetGroundPos(script.Points[i]) : script.Points[i];

                Handles.color = e.control ? Color.red : new Color(0, 0.9f, 1f, 0.9f);
                Handles.SphereHandleCap(0, currentPos, Quaternion.identity, HandleUtility.GetHandleSize(currentPos) * 0.12f, EventType.Repaint);

                if (!_isEditing || e.shift || e.control) continue;

                // Show PositionHandle only for the nearest point OR the currently dragged one
                if (i != _nearestPtIdx && i != _activeHandleIdx) continue;

                EditorGUI.BeginChangeCheck();
                Vector3 newPos = Handles.PositionHandle(currentPos, Quaternion.identity);
                if (EditorGUI.EndChangeCheck())
                {
                    _activeHandleIdx = i;
                    Undo.RecordObject(script, "Move Point");

                    if (script.SnapToTerrain) newPos.y = currentPos.y; // keep terrain Y

                    script.Points[i] = newPos;
                    script.ShapeActive = false;
                    script.TriggerRebuild();
                    EditorUtility.SetDirty(script);
                }
            }
        }

        private void GenerateShape(EasyFenceTool script, bool isCircle)
        {
            // Ask only when existing path was built manually
            if (script.Points.Count > 0 && !script.ShapeActive)
            {
                if (!EditorUtility.DisplayDialog(
                    "Replace Path?",
                    "This will replace the current path. Continue?",
                    "Yes", "No"))
                    return;
            }

            int[]  steps  = { 8, 16, 32, 64 };
            float  minLen = script.GetPhysicalMaxLength(); // largest panel must fit on every chord
            float  postR  = script.GetMaxPostRadius();

            // Center on existing points (e.g. first clicked point), otherwise scene-view ground centre
            Vector3 shapeCenter;
            if (script.Points.Count > 0)
            {
                Vector3 sum = Vector3.zero;
                foreach (var p in script.Points) sum += p;
                shapeCenter = sum / script.Points.Count;
                if (script.SnapToTerrain) shapeCenter = script.GetGroundPos(shapeCenter);
            }
            else
            {
                shapeCenter = GetSceneViewGroundPosition(script);
            }
            Undo.RecordObject(script.transform, isCircle ? "Generate Circle" : "Generate Square");
            script.transform.position = shapeCenter;

            Undo.RecordObject(script, isCircle ? "Generate Circle" : "Generate Square");
            script.ShapeIsCircle = isCircle;
            script.ShapeActive   = true;

            // Smart constraint before generating
            if (isCircle)
            {
                int maxPts = MaxPtsForRadius(script.ShapeSize, minLen, postR, steps);
                if (script.ShapeResolution > maxPts) script.ShapeResolution = maxPts;
                float minR = MinRadiusForPts(script.ShapeResolution, minLen, postR);
                if (script.ShapeSize < minR) script.ShapeSize = minR;
            }

            RegenerateAndRebuild(script);
            EditorUtility.SetDirty(script);
            SceneView.RepaintAll();
        }

        private void Bake()
        {
            foreach (EasyFenceTool s in targets)
            {
                var fencePrefabs = s.GetActiveFencePrefabs();
                var postPrefabs = s.GetActivePostPrefabs();
                
                if (s.Points.Count < 1) continue;
                if (fencePrefabs.Count == 0 && postPrefabs.Count == 0) continue;

                GameObject root = new GameObject($"{s.name}_Baked");
                Undo.RegisterCreatedObjectUndo(root, "Bake Fence");
                root.transform.position = s.transform.position;

                UnityEngine.Random.State oldState = UnityEngine.Random.state;
                float postRadius = s.GetMaxPostRadius();

                if (postPrefabs.Count > 0 && s.Points.Count > 0)
                {
                    for (int i = 0; i < s.Points.Count; i++)
                    {
                        if (!s.ShouldPlacePost(i, s.Points.Count, s.IsLoop)) continue;

                        UnityEngine.Random.InitState((i * 54321) ^ s.PostRandomSeed);

                        if (s.PostMissingChance > 0f && UnityEngine.Random.value < s.PostMissingChance) continue;

                        GameObject postPf = EasyFenceTool.PickWeighted(postPrefabs, s.GetPostPrefabWeight);
                        if (postPf == null) continue;

                        float pScale = UnityEngine.Random.Range(s.PostRandomScaleRange.x, s.PostRandomScaleRange.y);
                        Quaternion postRot = postPf.transform.rotation * Quaternion.Euler(s.PostRotationOffset);
                        postRot *= Quaternion.Euler(
                            UnityEngine.Random.Range(-s.PostRandomRotationJitter.x, s.PostRandomRotationJitter.x),
                            UnityEngine.Random.Range(-s.PostRandomRotationJitter.y, s.PostRandomRotationJitter.y),
                            UnityEngine.Random.Range(-s.PostRandomRotationJitter.z, s.PostRandomRotationJitter.z)
                        );

                        Vector3 pos = s.Points[i];
                        if (s.SnapToTerrain) pos = s.GetGroundPos(pos);
                        pos.y += s.VerticalOffset + s.PostVerticalOffset;
                        if (s.PostOffsetMode == OffsetMode.Standard)
                        {
                            // Standard = world-space offset: X = world X, Y = world Y, Z = world Z.
                            pos += s.PostPositionOffset;
                        }
                        else
                        {
                            pos += postRot * s.PostPositionOffset;
                        }

                        GameObject inst = (GameObject)PrefabUtility.InstantiatePrefab(postPf, root.transform);
                        inst.transform.SetPositionAndRotation(pos, postRot);
                        inst.transform.localScale = postPf.transform.localScale * pScale;
                    }
                }

                if (s.Points.Count >= 2 && fencePrefabs.Count > 0)
                {
                    int segs = s.IsLoop ? s.Points.Count : s.Points.Count - 1;

                    for (int i = 0; i < segs; i++)
                    {
                        Vector3 a = s.SnapToTerrain ? s.GetGroundPos(s.Points[i]) : s.Points[i];
                        Vector3 b = s.SnapToTerrain ? s.GetGroundPos(s.Points[(i + 1) % s.Points.Count]) : s.Points[(i + 1) % s.Points.Count];
                        
                        Vector3 dir = (b - a);
                        if (s.KeepVertical) dir.y = 0;
                        
                        float totalDist = dir.magnitude;
                        if (totalDist < 0.05f) continue;
                        dir.Normalize();

                        bool hasStartPost = s.ShouldPlacePost(i, s.Points.Count, s.IsLoop);
                        bool hasEndPost = s.ShouldPlacePost((i + 1) % s.Points.Count, s.Points.Count, s.IsLoop);

                        float startOffset = hasStartPost ? postRadius : 0f;
                        float endOffset = hasEndPost ? postRadius : 0f;

                        float currentDist = startOffset;
                        float maxDist = totalDist - endOffset;
                        int j = 0;

                        while (currentDist < maxDist)
                        {
                            UnityEngine.Random.InitState((i * 73856) ^ (j * 19349) ^ s.RandomSeed);
                            j++;
                            if (j > 1000) break;

                            GameObject pf = EasyFenceTool.PickWeighted(fencePrefabs, s.GetFencePrefabWeight);
                            if (!pf) break;
                            
                            float sScale = UnityEngine.Random.Range(s.RandomScaleRange.x, s.RandomScaleRange.y);
                            float actualLength = s.GetPrefabLength(pf, sScale);
                            if (actualLength < 0.05f) actualLength = 0.5f;

                            if (currentDist + actualLength > maxDist + 0.02f) break;

                            if (s.MissingSegmentChance > 0f && UnityEngine.Random.value < s.MissingSegmentChance)
                            {
                                currentDist += actualLength;
                                continue;
                            }

                            Vector3 basePos = a + dir * (currentDist + actualLength * 0.5f);
                            if (s.SnapToTerrain) basePos = s.GetGroundPos(basePos);
                            basePos.y += s.VerticalOffset;

                            bool forceVertB = s.KeepVertical || s.IsFencePrefabAlwaysVertical(pf);
                            Vector3 rotDir = forceVertB ? new Vector3(dir.x, 0f, dir.z).normalized : dir;
                            Quaternion rot = Quaternion.LookRotation(rotDir) * Quaternion.Euler(s.RotationOffset);
                            rot *= Quaternion.Euler(
                                forceVertB ? 0f : UnityEngine.Random.Range(-s.RandomRotationJitter.x, s.RandomRotationJitter.x),
                                UnityEngine.Random.Range(-s.RandomRotationJitter.y, s.RandomRotationJitter.y),
                                forceVertB ? 0f : UnityEngine.Random.Range(-s.RandomRotationJitter.z, s.RandomRotationJitter.z)
                            );

                            Vector3 pos;
                            if (s.FenceOffsetMode == OffsetMode.Standard)
                            {
                                pos = basePos + s.PositionOffset;
                            }
                            else
                            {
                                pos = basePos + rot * s.PositionOffset;
                            }

                            GameObject inst = (GameObject)PrefabUtility.InstantiatePrefab(pf, root.transform);
                            inst.transform.SetPositionAndRotation(pos, rot);
                            inst.transform.localScale = pf.transform.localScale * sScale;

                            currentDist += actualLength;
                        }
                    }
                }
                UnityEngine.Random.state = oldState;
            }
        }
    }
#endif
}