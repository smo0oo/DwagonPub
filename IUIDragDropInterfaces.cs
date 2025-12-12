using UnityEngine;

public interface IDragSource
{
    object GetItem();
    // The source now gets told WHERE the item was successfully dropped.
    void OnDropSuccess(IDropTarget target);
}

public interface IDropTarget
{
    bool CanReceiveDrop(object item);
    void OnDrop(object item);
}