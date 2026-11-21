using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Modo foco: esconde o painel de texto e centra/amplia o monumento. Carregar de novo volta ao normal.
/// Colocar no botão BtnFoco.
/// </summary>
public class FocoMonumento : MonoBehaviour
{
    [Header("Elementos")]
    public RectTransform colunaDireita;
    public RectTransform moldura;
    public RectTransform[] esconder;       // painel de texto (fundo + coluna esquerda)

    [Header("Ícone do botão")]
    public Image icone;
    public Sprite spriteExpandir;
    public Sprite spriteRecolher;

    [Header("Modo foco")]
    public float xMinFoco = 0.2f;
    public float xMaxFoco = 0.8f;
    public float tamanhoMolduraFoco = 840f;
    public float duracao = 0.35f;

    bool foco;
    bool guardado;
    float xMinNormal, xMaxNormal, tamanhoNormal;
    float t;                                // 0 = normal, 1 = foco
    Coroutine anim;

    void Guardar()
    {
        if (guardado || colunaDireita == null || moldura == null) return;
        xMinNormal = colunaDireita.anchorMin.x;
        xMaxNormal = colunaDireita.anchorMax.x;
        tamanhoNormal = moldura.sizeDelta.x;
        guardado = true;
    }

    void Awake() { Guardar(); }

    public void Alternar()
    {
        Guardar();
        foco = !foco;
        if (icone != null) icone.sprite = foco ? spriteRecolher : spriteExpandir;
        if (anim != null) StopCoroutine(anim);
        anim = StartCoroutine(Animar(foco ? 1f : 0f));
    }

    IEnumerator Animar(float alvo)
    {
        float inicio = t;
        float tempo = 0f;
        while (tempo < duracao)
        {
            tempo += Time.unscaledDeltaTime;
            t = Mathf.Lerp(inicio, alvo, Mathf.SmoothStep(0f, 1f, tempo / duracao));
            Aplicar();
            yield return null;
        }
        t = alvo;
        Aplicar();
        anim = null;
    }

    void Aplicar()
    {
        if (colunaDireita == null || moldura == null) return;
        var min = colunaDireita.anchorMin; min.x = Mathf.Lerp(xMinNormal, xMinFoco, t); colunaDireita.anchorMin = min;
        var max = colunaDireita.anchorMax; max.x = Mathf.Lerp(xMaxNormal, xMaxFoco, t); colunaDireita.anchorMax = max;
        float s = Mathf.Lerp(tamanhoNormal, tamanhoMolduraFoco, t);
        moldura.sizeDelta = new Vector2(s, s);

        foreach (var r in esconder)
        {
            if (r == null) continue;
            var cg = r.GetComponent<CanvasGroup>();
            if (cg == null) cg = r.gameObject.AddComponent<CanvasGroup>();
            cg.alpha = 1f - t;
            cg.blocksRaycasts = t < 0.5f;
            cg.interactable = t < 0.5f;
        }
    }

    // Ao fechar o painel, volta tudo ao normal
    void OnDisable()
    {
        if (anim != null) { StopCoroutine(anim); anim = null; }
        if (!guardado) return;
        foco = false; t = 0f;
        if (icone != null && spriteExpandir != null) icone.sprite = spriteExpandir;
        Aplicar();
    }
}
