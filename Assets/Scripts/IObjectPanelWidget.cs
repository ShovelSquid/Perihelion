// Anything inside an object's screen-space panel that shows information about that object.
// HitbarManager calls Bind once when it spawns the panel for an object; the widget keeps the
// reference and updates itself however it likes (Update, events, polling).
// Add new info to the panel by adding a component that implements this; the manager never changes.
public interface IObjectPanelWidget
{
    void Bind(Object obj);
}
