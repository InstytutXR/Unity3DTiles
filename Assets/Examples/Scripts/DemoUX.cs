using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Unity3DTiles;

class DemoUX : MonoBehaviour
{
#pragma warning disable 0649
    public AbstractTilesetBehaviour tileset;
    public TilesetStatsHud hud;
    public MouseFly mouseFly;
    public MouseOrbit mouseOrbit;
    public GameObject pointer;
#pragma warning restore 0649

    public float pointerRadiusPixels = 10;

    public bool drawSelectedBounds, drawParentBounds;

    public bool resetOrbitPivotOnNavChange = true;

    public bool enablePicking = true;

    private Unity3DTile selectedTile;
    private Stack<Unity3DTile> selectedStack = new Stack<Unity3DTile>();
    private Stack<Unity3DTileset> showStack = new Stack<Unity3DTileset>();
    
    private Vector3? lastMouse;
    private Vector2 mouseIntegral;

    private bool hasFocus = true;
    private bool didReset = false;

    private StringBuilder builder = new StringBuilder();

    private List<KeyCode> alphaNumerals = new List<KeyCode>()
    {
        KeyCode.Alpha0, KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4,
        KeyCode.Alpha5, KeyCode.Alpha6, KeyCode.Alpha7, KeyCode.Alpha8, KeyCode.Alpha9
    };

    private List<KeyCode> keypadNumerals = new List<KeyCode>()
    {
        KeyCode.Keypad0, KeyCode.Keypad1, KeyCode.Keypad2, KeyCode.Keypad3, KeyCode.Keypad4,
        KeyCode.Keypad5, KeyCode.Keypad6, KeyCode.Keypad7, KeyCode.Keypad8, KeyCode.Keypad9
    };
    
    public void OnApplicationFocus(bool focusStatus)
    {
        hasFocus = focusStatus;
    }

    public void Update()
    {
        if (tileset != null && tileset.Ready() && !didReset)
        {
            ResetView();
            didReset = true;
        }

        bool pointerActive = pointer != null && pointer.activeSelf;

        builder.Clear();

        builder.Append("\npress h to toggle HUD, v for default view, f to fit");

        MouseNavBase activeNav = null;
        if (mouseFly != null && mouseFly.enabled)
        {
            activeNav = mouseFly;
            builder.Append("\nfly navigation");
            if (mouseOrbit != null)
            {
                builder.Append(", press n to switch to orbit");
            }
            builder.Append("\nw/s/a/d/q/e to translate fwd/back/left/right/up/down");
        }
        else if (mouseOrbit != null && mouseOrbit.enabled)
        {
            activeNav = mouseOrbit;
            builder.Append("\norbit navigation");
            if (mouseFly != null)
            {
                builder.Append(", press n to switch to fly");
            }
            builder.Append("\npress c to rotate about ");
            builder.Append(pointerActive ? "pick point" : "centroid");
        }

        if (activeNav != null)
        {
            builder.Append("\ndrag mouse to rotate");
            builder.Append("\nmouse wheel to scale");
            if (activeNav.scaleModifier != MouseNavBase.Modifier.None)
            {
                builder.Append(" (or " + activeNav.scaleModifier + "-drag)");
            }
            builder.Append("\nright mouse to roll");
            if (activeNav.rollModifier != MouseNavBase.Modifier.None)
            {
                builder.Append(" (or " + activeNav.rollModifier + "-drag)");
            }
        }

        List<Unity3DTileset> tilesets = null;
        if (tileset is MultiTilesetBehaviour)
        {
            tilesets = ((MultiTilesetBehaviour)tileset).GetTilesets().ToList();
        }

        if (tilesets != null && tilesets.Count > 1)
        {
            string mods = "";
            if (tilesets.Count > 10)
            {
                mods += "[shift]+";
            }
            if (tilesets.Count > 20)
            {
                mods = "[ctrl]" + mods;
            }
            if (tilesets.Count > 40)
            {
                mods = "[alt]" + mods;
            }
            builder.Append("\npress " + mods + "0-9 to hide/show a tileset");
            int offset = 0;
            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
            {
                offset += 10;
            }
            if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
            {
                offset += 20;
            }
            if (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt))
            {
                offset += 40;
            }
            int idx = Mathf.Max(alphaNumerals.FindIndex(code => Input.GetKeyDown(code)),
                                keypadNumerals.FindIndex(code => Input.GetKeyDown(code)));
            if (idx >= 0)
            {
                idx += offset;
                if (idx < tilesets.Count)
                {
                    tilesets[idx].TilesetOptions.Show = !tilesets[idx].TilesetOptions.Show;
                    if (!tilesets[idx].TilesetOptions.Show)
                    {
                        showStack.Push(tilesets[idx]);
                        if (selectedTile != null && selectedTile.Tileset == tilesets[idx])
                        {
                            selectedTile = null;
                            selectedStack.Clear();
                        }
                    }
                }
            }
        }

        if (!showStack.Any(ts => !ts.TilesetOptions.Show))
        {
            showStack.Clear();
        }

        if (showStack.Count > 0)
        {
            builder.Append("\npress o to show last hidden tileset");
            if (Input.GetKeyDown(KeyCode.O))
            {
                while (showStack.Count > 0)
                {
                    var ts = showStack.Pop();
                    if (!ts.TilesetOptions.Show)
                    {
                        ts.TilesetOptions.Show = true;
                        break;
                    }
                }
            }
        }

        if (selectedTile != null)
        {
            float bv = selectedTile.BoundingVolume.Volume();
            float cbv = -1;
            if (selectedTile.ContentBoundingVolume != null)
            {
                cbv = selectedTile.ContentBoundingVolume.Volume();
            }

            builder.Append("\n");

            if (tilesets.Count > 0)
            {
                var sts = selectedTile.Tileset;
                builder.Append("\nselected tileset " + sts.TilesetOptions.Name + " (" + tilesets.FindIndex(ts => ts == sts) + ")");
            }

            builder.Append("\nselected tile " + selectedTile.Id + ", depth " + selectedTile.Depth);
            builder.Append(", " + selectedTile.Children.Count + " children");
            builder.Append(", geometric error " + selectedTile.GeometricError.ToString("F3"));
            
            builder.Append("\nbounds vol " + bv + ": " + selectedTile.BoundingVolume.SizeString());
            if (cbv >= 0 && cbv != bv)
            {
                builder.Append(", content vol " + cbv);
            }
            
            var tc = selectedTile.Content;
            if (tc != null && selectedTile.ContentState == Unity3DTileContentState.READY)
            {
                builder.Append("\n" + FmtKMG(tc.FaceCount) + " tris, " + FmtKMG(tc.PixelCount) + " pixels, ");
                builder.Append(tc.TextureCount + " textures, max " + tc.MaxTextureSize.x + "x" + tc.MaxTextureSize.y);
            }

            if (selectedTile.Parent != null)
            {
                builder.Append("\npress up/left/right");
                if (selectedTile.Children.Count > 0)
                {
                    builder.Append("/down");
                }
                builder.Append(" to select parent/sibling");
                if (selectedTile.Children.Count > 0)
                {
                    builder.Append("/child");
                }
            }
            else if (selectedTile.Children.Count > 0)
            {
                builder.Append("\npress down to select child");
            }

            if (selectedTile.Parent != null && Input.GetKeyDown(KeyCode.UpArrow))
            {
                selectedStack.Push(selectedTile);
                selectedTile = selectedTile.Parent;
            }

            if (selectedTile.Children.Count > 0 && Input.GetKeyDown(KeyCode.DownArrow))
            {
                selectedTile = selectedStack.Count > 0 ? selectedStack.Pop() : selectedTile.Children.First();
            }

            int sibling = Input.GetKeyDown(KeyCode.LeftArrow) ? -1 : Input.GetKeyDown(KeyCode.RightArrow) ? 1 : 0;
            if (selectedTile.Parent != null && sibling != 0)
            {
                var siblings = selectedTile.Parent.Children;
                int idx = siblings.FindIndex(c => c == selectedTile) + sibling;
                idx = idx < 0 ? siblings.Count - 1 : idx == siblings.Count ? 0 : idx;
                if (siblings[idx] != selectedTile)
                {
                    selectedStack.Clear();
                }
                selectedTile = siblings[idx];
            }

            builder.Append("\npress b to toggle bounds");

            if (Input.GetKeyDown(KeyCode.B))
            {
                if (!drawSelectedBounds && !drawParentBounds)
                {
                    drawSelectedBounds = true;
                }
                else if (drawSelectedBounds && !drawParentBounds)
                {
                    drawParentBounds = true;
                }
                else
                {
                    drawSelectedBounds = drawParentBounds = false;
                }
            }

            if (drawSelectedBounds)
            {
                selectedTile.BoundingVolume.DebugDraw(Color.magenta, selectedTile.Tileset.Behaviour.transform);
                if (cbv >= 0 && cbv != bv)
                {
                    selectedTile.ContentBoundingVolume.DebugDraw(Color.red, selectedTile.Tileset.Behaviour.transform);
                }
            }

            if (drawParentBounds && selectedTile.Parent != null)
            {
                var parent = selectedTile.Parent;
                float pbv = parent.BoundingVolume.Volume();
                float pcbv = parent.ContentBoundingVolume != null ? parent.ContentBoundingVolume.Volume() : -1;
                parent.BoundingVolume.DebugDraw(Color.cyan, selectedTile.Tileset.Behaviour.transform);
                if (pcbv >= 0 && pcbv != pbv)
                {
                    parent.ContentBoundingVolume.DebugDraw(Color.blue, selectedTile.Tileset.Behaviour.transform);
                }
            }

            if (tilesets != null && tilesets.Count > 0)
            {
                builder.Append("\npress i to hide tileset");
                if (Input.GetKeyDown(KeyCode.I))
                {
                    selectedTile.Tileset.TilesetOptions.Show = false;
                    showStack.Push(selectedTile.Tileset);
                    selectedTile = null;
                    selectedStack.Clear();
                }
            }

            builder.Append("\npress esc to clear selection");
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                selectedTile = null;
                selectedStack.Clear();
            }
        }

        if (hud != null)
        {
            hud.ExtraMessage = builder.ToString();
        }

        //toggle hud
        if (hud != null && Input.GetKeyDown(KeyCode.H))
        {
            hud.enabled = !hud.enabled;
        }

        if (Input.GetKeyDown(KeyCode.V))
        {
            ResetView();
        }

        if (Input.GetKeyDown(KeyCode.F))
        {
            FitView();
        }

        //toggle nav mode
        if (mouseFly != null && mouseOrbit != null && Input.GetKeyDown(KeyCode.N))
        {
            mouseFly.enabled = !mouseFly.enabled;
            mouseOrbit.enabled = !mouseOrbit.enabled;

            if (mouseOrbit.enabled && resetOrbitPivotOnNavChange)
            {
                if (Physics.Raycast(Camera.main.ScreenPointToRay(new Vector3(Screen.width / 2, Screen.height / 2)),
                                    out RaycastHit hit))
                {
                    mouseOrbit.pivot = hit.point;
                }
                else if (tileset && tileset.Ready())
                {
                    mouseOrbit.pivot = tileset.transform.TransformPoint(tileset.BoundingSphere().position);
                } 
            }
        }

        //set orbit nav pivot
        if (activeNav != null && activeNav == mouseOrbit && Input.GetKeyDown(KeyCode.C))
        {
            if (pointerActive)
            {
                mouseOrbit.pivot = pointer.transform.position;
            }
            else if (tileset && tileset.Ready())
            {
                mouseOrbit.pivot = tileset.transform.TransformPoint(tileset.BoundingSphere().position);
            } 
        }

        //handle mouse clicks
        if (enablePicking && hasFocus && !MouseNavBase.MouseOnUI())
        {
            if (Input.GetMouseButtonDown(0))
            {
                lastMouse = Input.mousePosition;
                mouseIntegral = Vector3.zero;
            }
            else if (Input.GetMouseButton(0) && lastMouse.HasValue)
            {
                var mouseDiff = Input.mousePosition - lastMouse.Value;
                mouseIntegral.x += Mathf.Abs(mouseDiff.x);
                mouseIntegral.y += Mathf.Abs(mouseDiff.y);
                lastMouse = Input.mousePosition;
            }
            else if (lastMouse.HasValue && mouseIntegral == Vector2.zero)
            {
                OnClick(Input.mousePosition);
                lastMouse = null;
            }
        }
        else
        {
            lastMouse = null;
        }

        //scale pointer to pointerRadiusPixels
        if (pointerActive)
        {
            var cam = Camera.main.transform;
            var w2cScale = cam.worldToLocalMatrix.lossyScale;
            var minScale = Mathf.Min(w2cScale.x, w2cScale.y, w2cScale.z);
            var vfov = Camera.main.fieldOfView * Mathf.Deg2Rad;
            var hfov = vfov * Camera.main.aspect;
            var maxRadPerPixel = Mathf.Max(vfov / Screen.height, hfov / Screen.width);
            pointer.transform.localScale = (1.0f / minScale) * Vector3.one *
                Vector3.Distance(pointer.transform.position, cam.position) *
                Mathf.Tan(pointerRadiusPixels * maxRadPerPixel);
        }

        //force rendering of selected tile
        if (tileset != null)
        {
            tileset.ClearForcedTiles();
        }
        if (selectedTile != null)
        {
            selectedTile.Tileset.Traversal.ForceTiles.Add(selectedTile);
        }
    }

    public void ResetView()
    {
        if (tileset != null)
        {
            var cam = Camera.main.transform;
            cam.position = tileset.SceneOptions.DefaultCameraPosition;
            cam.eulerAngles = tileset.SceneOptions.DefaultCameraRotation;
            cam.localScale = Vector3.one;

            if (mouseOrbit != null && tileset.Ready())
            {
                mouseOrbit.pivot = tileset.transform.TransformPoint(tileset.BoundingSphere().position);
            }
        }
    }

    public void FitView()
    {
        if (tileset != null && tileset.Ready())
        {
            var cam = Camera.main.transform;
            var sph = tileset.BoundingSphere();

            var ctrInWorld = tileset.transform.TransformPoint(sph.position);
            cam.Translate(Vector3.ProjectOnPlane(ctrInWorld - cam.position, cam.forward), Space.World);

            var tilesetToCam = tileset.transform.localToWorldMatrix * cam.worldToLocalMatrix; //row major compose l->r
            var t2cScale = tilesetToCam.lossyScale;
            var maxScale = Mathf.Max(t2cScale.x, t2cScale.y, t2cScale.z);
            var radiusInCam = sph.radius * maxScale;

            var vfov = Camera.main.fieldOfView * Mathf.Deg2Rad;
            var hfov = vfov * Camera.main.aspect;
            var minFov = Mathf.Min(vfov, hfov);

            var dist = radiusInCam / Mathf.Tan(minFov / 2);
            cam.Translate(cam.forward * (Vector3.Distance(cam.position, ctrInWorld) - dist), Space.World);

            if (mouseOrbit != null)
            {
                mouseOrbit.pivot = ctrInWorld;
            }
        }
    }

    public void OnClick(Vector3 mousePosition)
    {
        selectedTile = null;
        selectedStack.Clear();
        if (Physics.Raycast(Camera.main.ScreenPointToRay(mousePosition), out RaycastHit hit))
        {
            if (pointer != null)
            {
                pointer.SetActive(true);
                pointer.transform.position = hit.point;
            }
            var go = hit.collider.transform.gameObject;
            while (go != null)
            {
                var ti = go.GetComponent<TileInfo>();
                if (ti != null)
                {
                    selectedTile = ti.Tile;
                    break;
                }
                go = go.transform.parent != null ? go.transform.parent.gameObject : null;
            }
        }
        else
        {
            if (pointer != null)
            {
                pointer.SetActive(false);
            }
        }
    }

    public static string FmtKMG(float b, float k = 1e3f)
    {
        if (Mathf.Abs(b) < k) return b.ToString("f0");
        else if (Mathf.Abs(b) < k*k) return string.Format("{0:f1}k", b/k);
        else if (Mathf.Abs(b) < k*k*k) return string.Format("{0:f1}M", b/(k*k));
        else return string.Format("{0:f1}G", b/(k*k*k));
    }
}