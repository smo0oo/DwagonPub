using UnityEngine;
using System;

public interface IMovementHandler
{
    void ExecuteLeap(Vector3 destination, Action onLandAction);
    void ExecuteTeleport(Vector3 destination);
    // We can add Charge here in the future
}