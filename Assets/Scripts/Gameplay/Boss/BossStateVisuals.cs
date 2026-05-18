using System.Collections;
using UnityEngine;

[RequireComponent(typeof(BossController))]
public class BossStateVisuals : MonoBehaviour
{
    public void BeginPanting() { }
    public void EndPanting() { }
    public IEnumerator PlayCollapse() { yield break; }
    public IEnumerator PlayStandUp() { yield break; }
}
