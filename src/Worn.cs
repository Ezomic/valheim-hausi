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
        private int _hash;
        private GameObject _model;

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

                if (worn._hash != hash) worn.Change(__instance, hash);
            }
            catch (Exception error)
            {
                HausiPlugin.LogOnce("Could not draw a worn trophy: " + error);
            }
        }

        private void Change(VisEquipment vis, int hash)
        {
            _hash = hash;

            if (_model != null)
            {
                // Inactive at once, because Destroy waits for the end of the frame and
                // UpdateLodgroup below would otherwise put the dying renderers back on the list.
                _model.SetActive(false);
                Destroy(_model);
                _model = null;
            }

            if (hash != 0 && ObjectDB.instance != null)
            {
                GameObject prefab = ObjectDB.instance.GetItemPrefab(hash);
                if (prefab != null) _model = Build(prefab, vis);
            }

            RefreshLods(vis);
        }

        private static GameObject Build(GameObject prefab, VisEquipment vis)
        {
            Transform joint;
            Matrix4x4 bind;
            if (!BindOf(vis, HausiConfig.WornJoint.Value, out joint, out bind))
            {
                HausiPlugin.LogOnce("WornJoint names a bone the player does not have: "
                                    + HausiConfig.WornJoint.Value);
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

            float longest = Longest(model);
            float scale = longest > 0.001f ? HausiConfig.WornSize.Value / longest : 1f;

            // The body mesh's frame is not the character's. Measured on the male rig, it is z-up in
            // hundredths of a metre, scaled by 95 and turned 270 degrees, and the character is what
            // a position in the config means. So: character to mesh, then mesh to bone.
            Matrix4x4 meshToCharacter = vis.transform.worldToLocalMatrix * vis.m_bodyModel.transform.localToWorldMatrix;

            var place = new Vector3(HausiConfig.WornRight.Value, HausiConfig.WornUp.Value, HausiConfig.WornForward.Value);

            model.transform.SetParent(joint, false);
            model.transform.localPosition = bind.MultiplyPoint3x4(meshToCharacter.inverse.MultiplyPoint3x4(place));
            model.transform.localRotation = bind.rotation * Quaternion.Inverse(meshToCharacter.rotation)
                                            * Quaternion.Euler(0f, HausiConfig.WornTurn.Value, 0f);
            model.transform.localScale = Vector3.one * (scale / joint.lossyScale.y);

            return model;
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
