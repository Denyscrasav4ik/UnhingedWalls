using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace UnhingedWalls;

[BepInPlugin("denyscrasav4ik.thedumbfactory.unhingedwalls", "Unhinged Walls", "1.0.0")]
public class WallPlugin : BaseUnityPlugin
{
    private void Awake() => new Harmony("denyscrasav4ik.thedumbfactory.unhingedwalls").PatchAll();
}

[HarmonyPatch(typeof(PlayerManager), "Update")]
public class PlayerManager_Update_Patch
{
    private static GameObject lastProcessedObject;
    private static float lastProcessTime;

    private static void Postfix(PlayerManager __instance)
    {
        if (Singleton<CoreGameManager>.Instance == null || __instance.ec == null || __instance.pc == null)
            return;

        GameCamera gameCamera = Singleton<CoreGameManager>.Instance.GetCamera(__instance.playerNumber);

        if (gameCamera == null || gameCamera.camCom == null)
            return;

        Transform cam = gameCamera.camCom.transform;
        int layerMask = __instance.pc.ClickLayers;

        if (!Physics.Raycast(
                __instance.transform.position,
                cam.forward,
                out RaycastHit hit,
                __instance.pc.Reach,
                layerMask,
                QueryTriggerInteraction.Ignore))
            return;

        if (hit.collider.GetComponentInParent<UnhingedPhysicsWall>() != null || !HasTagInHierarchy(hit.collider.transform, "Wall"))
            return;

        Transform originalWall = hit.collider.transform;

        while (originalWall != null && !originalWall.CompareTag("Wall"))
            originalWall = originalWall.parent;

        if (originalWall == null)
            return;

        EnvironmentController ec = __instance.ec;
        Direction direction = Directions.DirFromVector3(hit.transform.forward, 5f);

        Vector3 normal = direction.ToVector3();
        normal.y = 0f;

        if (normal.sqrMagnitude > 0.001f)
            normal.Normalize();

        IntVector2 currentPosition = IntVector2.GetGridPosition(hit.transform.position - normal * 5f);

        if (!ec.ContainsCoordinates(currentPosition))
            return;

        Cell currentCell = ec.CellFromPosition(currentPosition);

        if (currentCell == null || currentCell.Null || !currentCell.HasWallInDirection(direction))
            return;

        GameObject hitObject = originalWall.gameObject;

        if (hitObject == lastProcessedObject && Time.time - lastProcessTime < 0.2f)
            return;

        lastProcessedObject = hitObject;
        lastProcessTime = Time.time;

        IntVector2 otherPosition = currentPosition + direction.ToIntVector2();

        ec.ConnectCells(currentCell.position, direction);
        UpdateMap(ec, currentCell, otherPosition);
        SpawnUnhingedWall(ec, currentCell, direction, cam, originalWall);
    }

    private static bool HasTagInHierarchy(Transform transform, string tag)
    {
        while (transform != null)
        {
            if (transform.CompareTag(tag))
                return true;

            transform = transform.parent;
        }

        return false;
    }

    private static void UpdateMap(EnvironmentController ec, Cell currentCell, IntVector2 otherPosition)
    {
        Map map = ec.map;

        if (map == null)
            map = Singleton<CoreGameManager>.Instance.GetComponentInChildren<Map>();

        if (map == null)
            return;

        UpdateMapCell(map, ec, currentCell.position);

        if (ec.ContainsCoordinates(otherPosition))
            UpdateMapCell(map, ec, otherPosition);
    }

    private static void UpdateMapCell(Map map, EnvironmentController ec, IntVector2 position)
    {
        if (!ec.ContainsCoordinates(position))
            return;

        Cell cell = ec.CellFromPosition(position);

        if (cell == null || cell.Null || cell.hideFromMap)
            return;

        map.UpdateTile(position.x, position.z, cell.ConstBin, cell.room);
    }

    private static void SpawnUnhingedWall(
        EnvironmentController ec,
        Cell currentCell,
        Direction direction,
        Transform camera,
        Transform originalWall)
    {
        if (currentCell == null || currentCell.Tile == null || originalWall == null)
            return;

        Tile tile = currentCell.Tile;

        GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = "UnhingedPhysicsWall";
        wall.AddComponent<UnhingedPhysicsWall>();

        SetWallAtlasUVs(wall);

        MeshRenderer renderer = wall.GetComponent<MeshRenderer>();

        if (tile.MeshRenderer != null)
        {
            renderer.sharedMaterials = tile.MeshRenderer.sharedMaterials;

            MaterialPropertyBlock propBlock = new MaterialPropertyBlock();
            tile.MeshRenderer.GetPropertyBlock(propBlock);
            renderer.SetPropertyBlock(propBlock);
        }

        wall.transform.localScale = new Vector3(10f, 10f, 2.5f);
        wall.transform.position = originalWall.position;
        wall.transform.rotation = originalWall.rotation;

        BoxCollider boxCollider = wall.GetComponent<BoxCollider>();
        if (boxCollider != null)
            Object.Destroy(boxCollider);

        MeshCollider meshCollider = wall.AddComponent<MeshCollider>();
        meshCollider.convex = true;

        Rigidbody rb = wall.AddComponent<Rigidbody>();
        rb.mass = 20f;
        rb.useGravity = true;
        rb.isKinematic = false;
        rb.constraints = RigidbodyConstraints.FreezePositionY;

        Vector3 forceDirection = camera.forward;
        forceDirection.y = 0f;

        if (forceDirection.sqrMagnitude > 0.001f)
        {
            forceDirection.Normalize();
            rb.AddForce(forceDirection * 8f, ForceMode.Impulse);
        }

        Physics.SyncTransforms();
    }

    private static void SetWallAtlasUVs(GameObject wall)
    {
        MeshFilter meshFilter = wall.GetComponent<MeshFilter>();

        if (meshFilter == null || meshFilter.mesh == null)
            return;

        Mesh mesh = meshFilter.mesh;
        Vector2[] uvs = mesh.uv;

        if (uvs == null || uvs.Length == 0)
            return;

        for (int i = 0; i < uvs.Length; i++)
        {
            uvs[i] = new Vector2(
                0.5f + uvs[i].x * 0.5f,
                0.5f + uvs[i].y * 0.5f
            );
        }

        mesh.uv = uvs;
    }
}

public class UnhingedPhysicsWall : MonoBehaviour
{
}
