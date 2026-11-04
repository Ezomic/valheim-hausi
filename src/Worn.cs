using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Hausi
{
    /// <summary>
    /// What a worn trophy looks like on the wearer, on every machine that can see them.
    ///
    /// <b>The same seam as gear, one step over.</b> VisEquipment.UpdateVisuals reads what each
    /// player has equipped off their ZDO every frame and attaches what changed. AttachArmor only
    /// looks for an item's attach_ children and a trophy has none, so this reads its own key in a
    /// postfix of the same method and hangs its own copy. Being a postfix there is what makes it
    /// visible to other players with no message of any kind: their machine runs this for your
    /// player object, off the ZDO you wrote.
    ///
    /// <b>A copy of the meshes and nothing else.</b> Instantiating the item would bring its
    /// ZNetView, Rigidbody, ItemDrop and colliders, and a collider on the hip is a character that
    /// hits itself. So each renderer becomes a bare MeshFilter and MeshRenderer sharing the donor's
    /// mesh and materials, which keeps the look exact and costs no new material.
    ///
    /// <b>Scaled by its longest side.</b> A boss's trophy is nearly a metre and a Neck's a hand's
    /// width, so a fixed scale would make one a backpack and the other a button.
    /// </summary>
    internal sealed class Worn : MonoBehaviour
    {
        private const string SashJoint = "Spine2";

        /// <summary>A build that could not resolve is tried again this much later, not every frame.</summary>
        private const int RetryFrames = 60;

        private const string Emission = "_EmissionColor";
        private const string EmissionKeyword = "_EMISSION";

        private int _hash;
        private int _nextTry;
        private GameObject _model;
        private string _prefabName;

        /// <summary>The quality the glow was last drawn for, -1 when the model has not been glowed yet.</summary>
        private int _glowQuality = -1;
        private string _glowSaid = "none";

        private static MaterialPropertyBlock _block;

        /// <summary>
        /// The token's material with emission switched on, one per borrowed material. The token wears
        /// BoneFragments' Standard material, and Standard only reads _EmissionColor with its _EMISSION
        /// keyword on, which no property block can set. So the copy is ours, made once, never written
        /// after, and every wearer's own colour goes through a property block on their own renderer.
        /// </summary>
        private static readonly Dictionary<Material, Material> Emissive = new Dictionary<Material, Material>();

        private static readonly HashSet<Material> Copies = new HashSet<Material>();

        internal static string Described(VisEquipment vis)
        {
            Worn worn;
            if (vis == null || !vis.TryGetComponent(out worn) || worn._model == null) return "none";

            var renderers = worn._model.GetComponentsInChildren<MeshRenderer>();
            Transform model = worn._model.transform;
            Vector3 onBody = vis.transform.InverseTransformPoint(model.position);

            return model.parent.name + " renderers=" + renderers.Length
                   + " longest=" + (Longest(worn._model) * model.lossyScale.y)
                       .ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)
                   + " at=" + onBody.ToString("F2").Replace(" ", "");
        }

        /// <summary>The colour this machine drew a player's token in, as #RRGGBB, or none.</summary>
        internal static string Glowing(VisEquipment vis)
        {
            Worn worn;
            return vis != null && vis.TryGetComponent(out worn) && worn._model != null ? worn._glowSaid : "none";
        }

        [HarmonyPatch(typeof(VisEquipment), "UpdateVisuals")]
        [HarmonyPostfix]
        private static void Visuals(VisEquipment __instance)
        {
            if (!__instance.m_isPlayer) return;

            try
            {
                ZNetView nview;
                if (!__instance.TryGetComponent(out nview) || !nview.IsValid()) return;

                int hash = nview.GetZDO().GetInt(Slot.WornKey);

                Worn worn;
                if (!__instance.TryGetComponent(out worn))
                {
                    if (hash == 0) return;
                    worn = __instance.gameObject.AddComponent<Worn>();
                }

                if (worn._hash != hash && Time.frameCount >= worn._nextTry)
                {
                    worn._nextTry = Time.frameCount + RetryFrames;
                    worn.Change(__instance, hash);
                }

                // Off this wearer's own ZDO, like the hash: that is what lets every other machine
                // draw your glow, and why nobody's glow can come from anybody else's sash.
                int quality = nview.GetZDO().GetInt(Slot.QualityKey);
                if (worn._model != null && worn._glowQuality != quality) worn.Glow(quality);
            }
            catch (Exception error)
            {
                HausiPlugin.LogOnce("Could not draw a worn trophy: " + error);
            }
        }

        private void Change(VisEquipment vis, int hash)
        {
            if (_model != null)
            {
                // Inactive at once, because Destroy waits for the end of the frame and
                // UpdateLodgroup below would otherwise put the dying renderers back on the list.
                _model.SetActive(false);
                Destroy(_model);
                _model = null;
            }

            _prefabName = null;
            _glowQuality = -1;
            _glowSaid = "none";

            if (hash != 0 && ObjectDB.instance != null)
            {
                GameObject prefab = ObjectDB.instance.GetItemPrefab(hash);
                if (prefab != null)
                {
                    _model = Build(prefab, vis);
                    _prefabName = prefab.name;
                }
            }

            // Remembered only once it has worked. A wearer can be seen before their item resolves, in
            // the stub ObjectDB of a session or before this mod's prefabs are back in a new world, and
            // remembering the hash regardless would leave them bare until they next changed it.
            if (hash == 0 || _model != null) _hash = hash;

            RefreshLods(vis);
        }

        private static GameObject Build(GameObject prefab, VisEquipment vis)
        {
            // A sash is modelled standing on the body, so its mesh is already where it goes, at its
            // own size, and only needs a bone to ride. The chest bone, because the strap has to move
            // with the ribs: the hip is the one part of it that will not follow, and that is why the
            // strap stops above the hip that moves most.
            bool sash = Sashes.BiomeOf(prefab.name) != null;
            string jointName = sash ? SashJoint : HausiConfig.WornJoint.Value;

            Transform joint;
            Matrix4x4 bind;
            if (!BindOf(vis, jointName, out joint, out bind))
            {
                HausiPlugin.LogOnce("A worn model names a bone the player does not have: " + jointName);
                return null;
            }

            var model = new GameObject("hausi_worn");
            model.layer = joint.gameObject.layer;

            int parts = 0;
            foreach (Renderer renderer in prefab.GetComponentsInChildren<Renderer>(true))
                if (CopyMesh(renderer, prefab.transform, model)) parts++;

            if (parts == 0)
            {
                Destroy(model);
                return null;
            }

            if (sash)
            {
                foreach (Transform part in model.transform)
                {
                    part.localPosition = Vector3.zero;
                    part.localRotation = Quaternion.identity;
                    part.localScale = Vector3.one;
                }
            }

            float longest = Longest(model);
            float scale = sash ? 1f : longest > 0.001f ? HausiConfig.WornSize.Value / longest : 1f;

            // The body mesh's frame is not the character's. Measured on the male rig, it is z-up in
            // hundredths of a metre, scaled by 95 and turned 270 degrees, and the character is what
            // a position in the config means. So: character to mesh, then mesh to bone.
            Matrix4x4 meshToCharacter = vis.transform.worldToLocalMatrix * vis.m_bodyModel.transform.localToWorldMatrix;

            Vector3 place = sash
                ? Vector3.zero
                : new Vector3(HausiConfig.WornRight.Value, HausiConfig.WornUp.Value, HausiConfig.WornForward.Value);
            float turn = sash ? 0f : HausiConfig.WornTurn.Value;

            model.transform.SetParent(joint, false);
            model.transform.localPosition = bind.MultiplyPoint3x4(meshToCharacter.inverse.MultiplyPoint3x4(place));
            model.transform.localRotation = bind.rotation * Quaternion.Inverse(meshToCharacter.rotation)
                                            * Quaternion.Euler(0f, turn, 0f);
            model.transform.localScale = Vector3.one * (scale / joint.lossyScale.y);

            return model;
        }

        /// <summary>
        /// An upgraded sash's token glows faintly in its biome's colour, and nothing else does. No
        /// Light, on purpose: a light per wearer is a cost every machine pays for every player in view.
        /// </summary>
        private void Glow(int quality)
        {
            _glowQuality = quality;
            _glowSaid = "none";

            int index = Sashes.TokenSubmeshOf(_prefabName);
            if (index < 0) return;

            Color colour = Color.black;
            bool lit = quality >= 2 && GlowColour(Sashes.BiomeOf(_prefabName), out colour);

            if (_block == null) _block = new MaterialPropertyBlock();
            _block.Clear();
            _block.SetColor(Emission, lit ? colour : Color.black);

            foreach (MeshRenderer renderer in _model.GetComponentsInChildren<MeshRenderer>(true))
            {
                Material[] materials = renderer.sharedMaterials;
                if (index >= materials.Length || materials[index] == null) continue;

                if (lit)
                {
                    materials[index] = EmissiveOf(materials[index]);
                    renderer.sharedMaterials = materials;
                }

                renderer.SetPropertyBlock(_block, index);
            }

            if (lit) _glowSaid = "#" + ColorUtility.ToHtmlStringRGB(colour);
        }

        private static Material EmissiveOf(Material source)
        {
            // A renderer lit once already wears the copy, and a sash taken from quality 2 to 1 and
            // back keeps it: copying the copy would make one more material per cycle, on every
            // machine that sees that player.
            if (Copies.Contains(source)) return source;
            if (Emissive.TryGetValue(source, out Material copy) && copy != null) return copy;

            copy = new Material(source) { name = source.name + " (hausi glow)" };
            copy.EnableKeyword(EmissionKeyword);
            copy.SetColor(Emission, Color.black);
            Emissive[source] = copy;
            Copies.Add(copy);

            return copy;
        }

        /// <summary>A biome's GlowColours entry. False for no entry or one that does not parse.</summary>
        private static bool GlowColour(string biome, out Color colour)
        {
            colour = Color.black;
            if (biome == null) return false;

            foreach (string entry in (HausiConfig.GlowColours.Value ?? "").Split(','))
            {
                string[] pair = entry.Split(':');
                if (pair.Length != 2 || pair[0].Trim().ToLowerInvariant() != biome) continue;

                if (ColorUtility.TryParseHtmlString(pair[1].Trim(), out colour)) return true;

                HausiPlugin.LogOnce("GlowColours gives " + biome + " '" + pair[1].Trim() + "', which is not a "
                                    + "colour, so its upgraded sash does not glow. Write it as #RRGGBB.");
                return false;
            }

            return false;
        }

        /// <summary>
        /// The matrix from the body mesh's own frame, feet at the origin and metres up, into this
        /// bone's frame. Placing through it means a position is the same place on the body whatever
        /// the animation was doing at the moment of equipping. The first version read the bone's
        /// live rotation, so a trophy put on mid-stride hung at that angle for good. It is also what
        /// copes with the rig being scaled by a hundred.
        /// </summary>
        private static bool BindOf(VisEquipment vis, string jointName, out Transform joint, out Matrix4x4 bind)
        {
            joint = null;
            bind = Matrix4x4.identity;

            SkinnedMeshRenderer body = vis.m_bodyModel;
            if (body == null || body.sharedMesh == null) return false;

            Transform[] bones = body.bones;
            Matrix4x4[] poses = body.sharedMesh.bindposes;

            for (int i = 0; i < bones.Length && i < poses.Length; i++)
            {
                if (bones[i] == null || bones[i].name != jointName) continue;

                joint = bones[i];
                bind = poses[i];
                return true;
            }

            return false;
        }

        /// <summary>
        /// One renderer of the item as a bare mesh under the model, at the place it holds on the
        /// prefab. Skinned trophies keep their bind pose, which is the shape they are drawn in.
        /// </summary>
        private static bool CopyMesh(Renderer renderer, Transform root, GameObject model)
        {
            Mesh mesh = null;

            MeshFilter filter;
            SkinnedMeshRenderer skinned = renderer as SkinnedMeshRenderer;

            if (skinned != null) mesh = skinned.sharedMesh;
            else if (renderer is MeshRenderer && renderer.TryGetComponent(out filter)) mesh = filter.sharedMesh;

            if (mesh == null) return false;

            var part = new GameObject(renderer.name);
            part.layer = model.layer;
            part.transform.SetParent(model.transform, false);
            part.transform.localPosition = root.InverseTransformPoint(renderer.transform.position);
            part.transform.localRotation = Quaternion.Inverse(root.rotation) * renderer.transform.rotation;
            part.transform.localScale = renderer.transform.lossyScale;

            part.AddComponent<MeshFilter>().sharedMesh = mesh;
            part.AddComponent<MeshRenderer>().sharedMaterials = renderer.sharedMaterials;

            return true;
        }

        /// <summary>The longest side of the model's own bounds, in its own frame and scale.</summary>
        private static float Longest(GameObject model)
        {
            bool any = false;
            var bounds = new Bounds();
            Matrix4x4 toModel = model.transform.worldToLocalMatrix;

            foreach (MeshFilter filter in model.GetComponentsInChildren<MeshFilter>(true))
            {
                Matrix4x4 local = toModel * filter.transform.localToWorldMatrix;
                Bounds mesh = filter.sharedMesh.bounds;

                for (int i = 0; i < 8; i++)
                {
                    var corner = new Vector3(
                        mesh.center.x + ((i & 1) == 0 ? -1 : 1) * mesh.extents.x,
                        mesh.center.y + ((i & 2) == 0 ? -1 : 1) * mesh.extents.y,
                        mesh.center.z + ((i & 4) == 0 ? -1 : 1) * mesh.extents.z);

                    Vector3 point = local.MultiplyPoint3x4(corner);

                    if (!any) bounds = new Bounds(point, Vector3.zero);
                    else bounds.Encapsulate(point);

                    any = true;
                }
            }

            return Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
        }

        private static System.Reflection.MethodInfo _updateLods;
        private static bool _lodsBound;

        /// <summary>
        /// Puts the new renderers on the player's LODGroup, because that group is how the game hides
        /// a character: a trophy left off it would hang in the air after its wearer had gone. Private
        /// and reached by name, so a rename costs the hiding and never the model.
        /// </summary>
        private static void RefreshLods(VisEquipment vis)
        {
            if (!_lodsBound)
            {
                _lodsBound = true;
                _updateLods = AccessTools.Method(typeof(VisEquipment), "UpdateLodgroup");
            }

            if (_updateLods != null) _updateLods.Invoke(vis, null);
        }
    }
}
