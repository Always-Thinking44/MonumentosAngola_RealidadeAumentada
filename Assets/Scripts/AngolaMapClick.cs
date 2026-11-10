using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Recebe o toque no RawImage do mapa e converte-o num raio da câmara de pré-visualização.
[RequireComponent(typeof(RawImage))]
public class AngolaMapClick : MonoBehaviour, IPointerClickHandler
{
    public Camera cameraPreview;
    public AngolaUI angolaUI;

    public void OnPointerClick(PointerEventData eventData)
    {
        if (cameraPreview == null || angolaUI == null) return;

        var rt = (RectTransform)transform;
        Vector2 local;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, eventData.position, eventData.pressEventCamera, out local))
            return;

        // Normaliza para o espaço do RenderTexture (0..1)
        float u = (local.x - rt.rect.x) / rt.rect.width;
        float v = (local.y - rt.rect.y) / rt.rect.height;

        angolaUI.ClicarNoMapa(u, v, cameraPreview);
    }
}
