using UnityEngine;
namespace Alterverse.Visuals
{
public sealed class ChronoRingMotion:MonoBehaviour
{
private float degreesPerSecond=18f;public void Configure(float speed){degreesPerSecond=speed;}
private void Update(){if(Time.timeScale<=0f)return;transform.Rotate(Vector3.up,degreesPerSecond*Time.deltaTime,Space.Self);}
}
}