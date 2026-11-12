using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Colocar num GameObject da cena (ex.: o próprio Canvas). Só pode haver um.
public class MonumentUI : MonoBehaviour
{
    public static MonumentUI Instance { get; private set; }

    [Header("UI")]
    public GameObject painel;           // painel principal (começa desactivado)
    public TMP_Text titulo;             // nome do monumento
    public TMP_Text infoTexto;          // só a descrição
    public TMP_Text dataTexto;          // ao lado do ícone de calendário
    public TMP_Text localTexto;         // ao lado do ícone de localização

    [Header("Botão play/pausa")]
    public Image iconePlayPausa;
    public Sprite spritePausa;          // mostrado enquanto o áudio toca
    public Sprite spritePlay;           // mostrado quando está em pausa

    [Header("Pré-visualização 3D")]
    public Transform palco;             // posição onde o modelo é instanciado
    public string nomeLayer = "Preview";
    public float velocidade = 40f;
    public float tamanhoAlvo = 2f;      // maior dimensão do modelo, em unidades

    [Header("Escala (zoom do modelo)")]
    [Range(0.2f, 1f)] public float escalaMinima = 0.4f;   // menor multiplicador
    [Range(1.1f, 5f)] public float escalaMaxima = 2.5f;  // maior multiplicador
    public float passoEscala = 1.2f;                      // factor aplicado por clique

    [Header("Áudio")]
    public AudioSource fundo;
    public AudioSource voz;
    [Range(0f, 1f)] public float volumeFundo = 0.25f;

    [Header("Comportamento")]
    public bool esconderAoPerderTarget = true;

    MonumentTarget atual;
    GameObject modeloAtual;
    int direcao = 1;
    bool pausado;
    Vector3 escalaBase;      // escala normalizada no CriarModelo (equivale ao factor 1)
    float fatorEscala = 1f;  // multiplicador actual aplicado pelos botões de zoom

    void Awake()
    {
        Instance = this;
        painel.SetActive(false);
    }

    void Update()
    {
        // Roda o próprio modelo no eixo Y (a câmara de preview fica parada)
        if (modeloAtual != null)
            modeloAtual.transform.RotateAround(palco.position, Vector3.up, velocidade * direcao * Time.deltaTime);
    }

    public void Mostrar(MonumentTarget t)
    {
        if (atual == t && painel.activeSelf) return;
        atual = t;

        titulo.text = t.nome;
        infoTexto.text = t.descricao;
        dataTexto.text = t.data;
        localTexto.text = t.localizacao;
        painel.SetActive(true);

        CriarModelo(t.modeloPrefab, t.rotacaoInicial);
        IniciarAudio(t);
    }

    public void AoPerderTarget(MonumentTarget t)
    {
        if (atual != t) return;

        Fechar();
    }

    // ---- Botões ----

    // Play / pausa (música e narração juntas)
    public void AlternarAudio()
    {
        pausado = !pausado;
        if (pausado) { fundo.Pause(); voz.Pause(); }
        else { fundo.UnPause(); voz.UnPause(); }
        AtualizarIcone();
    }

    // |◀ recomeça a narração do início
    public void ReiniciarNarracao()
    {
        if (voz.clip == null) return;
        pausado = false;
        fundo.UnPause();
        voz.Stop();
        voz.Play();
        AtualizarIcone();
    }

    // ▶| termina a narração (fica só a música de fundo)
    public void FimNarracao()
    {
        voz.Stop();
    }

    // Setas de rotação: a frente do modelo move-se para a esquerda / direita
    public void RodarEsquerda() => direcao = 1;
    public void RodarDireita() => direcao = -1;

    // Zoom: aumenta / diminui o tamanho do modelo (com limites)
    public void AumentarEscala() => AplicarEscala(fatorEscala * passoEscala);
    public void DiminuirEscala() => AplicarEscala(fatorEscala / passoEscala);
    public void ReiniciarEscala() => AplicarEscala(1f);

    public void Fechar()
    {
        fundo.Stop();
        voz.Stop();
        if (modeloAtual != null) Destroy(modeloAtual);
        modeloAtual = null;
        atual = null;
        painel.SetActive(false);
    }

    // ---- Internos ----

    void CriarModelo(GameObject prefab, Vector3 rotacao)
    {
        if (modeloAtual != null) Destroy(modeloAtual);
        direcao = 1;
        fatorEscala = 1f;
        escalaBase = Vector3.zero;
        if (prefab == null) return;

        modeloAtual = Instantiate(prefab, palco);
        modeloAtual.transform.localPosition = Vector3.zero;
        modeloAtual.transform.localRotation = Quaternion.Euler(rotacao);
        modeloAtual.transform.localScale = Vector3.one;
        DefinirLayer(modeloAtual, LayerMask.NameToLayer(nomeLayer));

        // Ajusta tamanho e centra no palco, para rodar sobre o próprio centro
        var rends = modeloAtual.GetComponentsInChildren<Renderer>();
        if (rends.Length == 0) return;

        Bounds b = CalcularBounds(rends);
        float maior = Mathf.Max(b.size.x, b.size.y, b.size.z);
        if (maior > 0f) modeloAtual.transform.localScale *= tamanhoAlvo / maior;

        b = CalcularBounds(rends);
        modeloAtual.transform.position += palco.position - b.center;

        escalaBase = modeloAtual.transform.localScale;
    }

    // Aplica o factor de zoom e volta a centrar o modelo no palco
    void AplicarEscala(float novoFator)
    {
        if (modeloAtual == null || escalaBase == Vector3.zero) return;
        novoFator = Mathf.Clamp(novoFator, escalaMinima, escalaMaxima);

        var t = modeloAtual.transform;
        var rends = t.GetComponentsInChildren<Renderer>();
        if (rends.Length == 0) return;

        Vector3 centroAntes = CalcularBounds(rends).center;
        t.localScale = escalaBase * novoFator;
        fatorEscala = novoFator;

        // Re-centra para que o modelo continue a rodar sobre o mesmo ponto
        t.position += centroAntes - CalcularBounds(rends).center;
    }

    static Bounds CalcularBounds(Renderer[] rends)
    {
        Bounds b = rends[0].bounds;
        for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
        return b;
    }

    static void DefinirLayer(GameObject go, int layer)
    {
        foreach (var t in go.GetComponentsInChildren<Transform>(true))
            t.gameObject.layer = layer;
    }

    void IniciarAudio(MonumentTarget t)
    {
        pausado = false;

        fundo.Stop();
        fundo.clip = t.musicaFundo;
        fundo.loop = true;
        fundo.volume = volumeFundo;
        if (fundo.clip != null) fundo.Play();

        voz.Stop();
        voz.clip = t.narracao;
        voz.loop = false;
        if (voz.clip != null) voz.Play();

        AtualizarIcone();
    }

    void AtualizarIcone()
    {
        if (iconePlayPausa != null)
            iconePlayPausa.sprite = pausado ? spritePlay : spritePausa;
    }
}
