using UnityEngine;

public class MagnetAreaMarker : MonoBehaviour
{
    [SerializeField] private MagnetArea owner;

    public MagnetArea Owner => owner;

    public void SetOwner(MagnetArea magnetArea)
    {
        owner = magnetArea;
    }
}
