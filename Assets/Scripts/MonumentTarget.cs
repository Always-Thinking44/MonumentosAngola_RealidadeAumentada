using UnityEngine;
using Vuforia;

// Colocar em cada Image Target (ao lado do ObserverBehaviour).
[RequireComponent(typeof(ObserverBehaviour))]
public class MonumentTarget : MonoBehaviour
{
    [Header("Informação")]
    public string nome;
    [TextArea(4, 12)] public string descricao;
    public string data;
    public string localizacao;

    [Header("3D e áudio")]
    public GameObject modeloPrefab;
    [Tooltip("Corrige a orientação do modelo (ex.: palanca 180,0,0)")]
    public Vector3 rotacaoInicial;
    public AudioClip narracao;
    public AudioClip musicaFundo;

    ObserverBehaviour observer;

    void Awake()
    {
        observer = GetComponent<ObserverBehaviour>();
        observer.OnTargetStatusChanged += OnStatusChanged;
    }

    void OnDestroy()
    {
        if (observer != null)
            observer.OnTargetStatusChanged -= OnStatusChanged;
    }

    void OnStatusChanged(ObserverBehaviour behaviour, TargetStatus status)
    {
        // Só TRACKED: EXTENDED_TRACKED mantinha o painel depois de perder a imagem
        bool detectado = status.Status == Status.TRACKED;

        if (detectado)
            MonumentUI.Instance.Mostrar(this);
        else
            MonumentUI.Instance.AoPerderTarget(this);
    }
}
