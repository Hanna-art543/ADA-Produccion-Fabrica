using Fabrica.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Fabrica.UI
{
    /// <summary>Una barra del Gantt: muestra el tooltip y resalta la misma pieza al pasar el mouse.</summary>
    public class BarraGantt : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerMoveHandler, IPointerClickHandler
    {
        public Assignment asignacion;
        public GanttChart gantt;
        public Image imagen;
        public Color colorBase;

        public void OnPointerEnter(PointerEventData e)
        {
            if (gantt != null) gantt.AlEntrarBarra(this, e.position);
        }

        public void OnPointerMove(PointerEventData e)
        {
            if (gantt != null) gantt.AlMoverBarra(e.position);
        }

        public void OnPointerExit(PointerEventData e)
        {
            if (gantt != null) gantt.AlSalirBarra(this);
        }

        public void OnPointerClick(PointerEventData e)
        {
            if (gantt != null) gantt.AlClicEnTiempo(e);
        }
    }
}
