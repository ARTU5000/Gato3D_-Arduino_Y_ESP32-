using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO.Ports;

public class GameManager : MonoBehaviour
{
    // Singleton
    public static GameManager Instance { get; private set; }

    // Tamaño del tablero
    private const int GRID_SIZE = 3;
    private char[,,] board = new char[GRID_SIZE, GRID_SIZE, GRID_SIZE];

    // Jugador actual (1 = 'X', 2 = 'O', 3 = 'T')
    public int currentPlayer = 1;

    // Prefabs para los cubos de los jugadores
    public GameObject player1CubePrefab;
    public GameObject player2CubePrefab;
    public GameObject player3CubePrefab;
    public GameObject gridLinePrefab;

    // Posiciones del tablero
    private Vector3[,,] boardPositions = new Vector3[GRID_SIZE, GRID_SIZE, GRID_SIZE];

    // Estado del juego
    private bool gameOver = false;
    private int winner = 0;
    private float gameOverTime = 0f;

    // Animaciones
    private List<GameObject> activeCubes = new List<GameObject>();
    private Dictionary<GameObject, Vector3> movingCubes = new Dictionary<GameObject, Vector3>();

    // Cámara
    public Camera mainCamera;
    public float cameraDistance = 10f;
    private float yaw = 0f;
    private float pitch = 30f;
    private Vector3 lastMousePosition;


    private int? selectedIndex = null;

    //Arduino
    SerialPort serialPort;
    public string portName = "COM5";
    public int baudRate = 9600;

    public bool arduinoButtonPressed = false;

    //ESP32
    private SerialPort bluetoothStream;
    public string bluetoothPort = "COM7"; // Puerto Bluetooth (ESP32)
    public int bluetoothBaudRate = 9600;

    List<GameObject> activeWinLines = new List<GameObject>();

    // Teclas para cada posición
    private KeyCode[] keyMap = new KeyCode[27]
    {
        KeyCode.Q, KeyCode.W, KeyCode.E,
        KeyCode.A, KeyCode.S, KeyCode.D,
        KeyCode.Z, KeyCode.X, KeyCode.C,
        KeyCode.R, KeyCode.T, KeyCode.Y,
        KeyCode.F, KeyCode.G, KeyCode.H,
        KeyCode.V, KeyCode.B, KeyCode.N,
        KeyCode.U, KeyCode.I, KeyCode.O,
        KeyCode.J, KeyCode.K, KeyCode.L,
        KeyCode.M, KeyCode.Comma, KeyCode.Period
    };

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }

        InitializeBoard();
        CreateGrid();
        CalculateBoardPositions();


        serialPort = new SerialPort(portName, baudRate);
        serialPort.Open();
        serialPort.ReadTimeout = 100;
    }

    void Start()
    {
        // Inicializar conexión Bluetooth
        try
        {
            bluetoothStream = new SerialPort(bluetoothPort, bluetoothBaudRate);
            bluetoothStream.ReadTimeout = 50;
            bluetoothStream.Open();
            Debug.Log("Conexión Bluetooth establecida");
            
            SendPlayerToESP32();
        }
        catch (System.Exception e)
        {
            Debug.LogError("Error al conectar Bluetooth: " + e.Message);
        }
    }

    private void InitializeBoard()
    {
        foreach (GameObject cube in activeWinLines)
        {
            Destroy(cube);
        }

        for (int z = 0; z < GRID_SIZE; z++)
        {
            for (int y = 0; y < GRID_SIZE; y++)
            {
                for (int x = 0; x < GRID_SIZE; x++)
                {
                    board[z, y, x] = ' ';
                }
            }
        }
    }

    private void CreateGrid()
    {
        // Crear líneas del grid
        for (int i = 0; i <= GRID_SIZE; i++)
        {
            float pos = -1.0f + (i * 2.0f / GRID_SIZE);

            // Líneas en X
            CreateGridLine(new Vector3(pos, -1.0f, -1.0f), new Vector3(pos, -1.0f, 1.0f));
            CreateGridLine(new Vector3(pos, 1.0f, -1.0f), new Vector3(pos, 1.0f, 1.0f));

            // Líneas en Y
            CreateGridLine(new Vector3(-1.0f, pos, -1.0f), new Vector3(1.0f, pos, -1.0f));
            CreateGridLine(new Vector3(-1.0f, pos, 1.0f), new Vector3(1.0f, pos, 1.0f));

            // Líneas en Z
            CreateGridLine(new Vector3(-1.0f, -1.0f, pos), new Vector3(1.0f, -1.0f, pos));
            CreateGridLine(new Vector3(-1.0f, 1.0f, pos), new Vector3(1.0f, 1.0f, pos));
        }
    }

    private void CreateGridLine(Vector3 start, Vector3 end)
    {
        GameObject line = Instantiate(gridLinePrefab);
        LineRenderer lr = line.GetComponent<LineRenderer>();
        lr.SetPosition(0, start);
        lr.SetPosition(1, end);
    }

    private void CalculateBoardPositions()
    {
        for (int z = 0; z < GRID_SIZE; z++)
        {
            for (int y = 0; y < GRID_SIZE; y++)
            {
                for (int x = 0; x < GRID_SIZE; x++)
                {
                    boardPositions[z, y, x] = new Vector3(
                        -1.0f + (x * 2.0f / GRID_SIZE) + 0.33f,
                        1.0f - (y * 2.0f / GRID_SIZE) - 0.33f,
                        -1.0f + (z * 2.0f / GRID_SIZE) + 0.33f
                    );
                }
            }
        }
    }

    private void Update()
    {
        if (serialPort.IsOpen)
        {
            try
            {
                string data = serialPort.ReadLine();
                if (data.Contains("BUTTON_PRESSED"))
                {
                    if (!selectedIndex.HasValue) return;
                    arduinoButtonPressed = true;
                }
            }
            catch (System.Exception) { }
        }

        HandleCameraRotation();
        ProcessInput();
        UpdateMovingCubes();
        CheckGameOver();
    }

    private void HandleCameraRotation()
    {
        if (Input.GetMouseButtonDown(0))
        {
            lastMousePosition = Input.mousePosition;
        }

        if (Input.GetMouseButton(0))
        {
            Vector3 delta = Input.mousePosition - lastMousePosition;
            yaw += delta.x * 0.2f;
            pitch -= delta.y * 0.2f;
            pitch = Mathf.Clamp(pitch, -89f, 89f);
            lastMousePosition = Input.mousePosition;

            UpdateCameraPosition();
        }
    }

    private void UpdateCameraPosition()
    {
        float camX = cameraDistance * Mathf.Cos(Mathf.Deg2Rad * yaw) * Mathf.Cos(Mathf.Deg2Rad * pitch);
        float camY = cameraDistance * Mathf.Sin(Mathf.Deg2Rad * pitch);
        float camZ = cameraDistance * Mathf.Sin(Mathf.Deg2Rad * yaw) * Mathf.Cos(Mathf.Deg2Rad * pitch);

        mainCamera.transform.position = new Vector3(camX, camY, camZ);
        mainCamera.transform.LookAt(Vector3.zero);
    }

    private void ProcessInput()
    {
        if (gameOver) return;

        for (int i = 0; i < keyMap.Length; i++)
        {
            if (Input.GetKeyDown(keyMap[i]))
            {
                selectedIndex = i;
                Debug.Log($"Posición seleccionada: {i}. Presiona Enter para confirmar.");
                break;
            }
        }

        if (Input.GetKeyDown(KeyCode.Return) || arduinoButtonPressed)
        {
            if (!selectedIndex.HasValue) return;
            int i = selectedIndex.Value;
            int z = i / 9;
            int y = (i % 9) / 3;
            int x = (i % 9) % 3;

            if (board[z, y, x] == ' ')
            {
                PlacePiece(z, y, x);
            }
            else
            {
                Debug.Log("Posición ocupada. Selecciona otra.");
            }

            selectedIndex = null; // Resetear para la próxima selección
            arduinoButtonPressed = false;
        }
    }

    private void PlacePiece(int z, int y, int x)
    {
        // Posición inicial (centro arriba del tablero)
        Vector3 startPosition = new Vector3(0f, 2f, 0f);
        Vector3 endPosition = boardPositions[z, y, x];

        // Crear el cubo del jugador
        GameObject cubePrefab = currentPlayer == 1 ? player1CubePrefab :
                              currentPlayer == 2 ? player2CubePrefab : player3CubePrefab;
        GameObject cube = Instantiate(cubePrefab, startPosition, Quaternion.identity);
        cube.transform.localScale = new Vector3(0.2f, 0.2f, 0.2f);

        // Configurar animación
        movingCubes.Add(cube, endPosition);
        activeCubes.Add(cube);

        // Actualizar tablero
        board[z, y, x] = currentPlayer == 1 ? 'X' : currentPlayer == 2 ? 'O' : 'T';

        // Verificar victoria
        if (CheckWin(board[z, y, x]))
        {
            gameOver = true;
            winner = currentPlayer;
            gameOverTime = Time.time;
            Debug.Log($"¡Jugador {currentPlayer} gana!");
            for (int i = 0; i < 20; i++)
            {
                currentPlayer = 5;
                SendPlayerToESP32();
                currentPlayer = winner;
                SendPlayerToESP32();
            }
            return;
        }

        // Cambiar jugador
        currentPlayer = (currentPlayer % 3) + 1;
        SendPlayerToESP32();
    }

    private void SendPlayerToESP32()
    {
        if (bluetoothStream != null && bluetoothStream.IsOpen)
        {
            try
            {
                bluetoothStream.Write(currentPlayer.ToString());
                Debug.Log("Enviado a ESP32: " + currentPlayer);
            }
            catch (System.Exception e)
            {
                Debug.LogError("Error al enviar datos: " + e.Message);
            }
        }
    }

    private void UpdateMovingCubes()
    {
        List<GameObject> toRemove = new List<GameObject>();

        foreach (var kvp in movingCubes)
        {
            GameObject cube = kvp.Key;
            Vector3 targetPosition = kvp.Value;

            // Mover el cubo hacia la posición objetivo
            cube.transform.position = Vector3.Lerp(cube.transform.position, targetPosition, Time.deltaTime * 5f);

            // Si está lo suficientemente cerca, considerarlo llegado
            if (Vector3.Distance(cube.transform.position, targetPosition) < 0.01f)
            {
                cube.transform.position = targetPosition;
                toRemove.Add(cube);
            }
        }

        // Eliminar cubos que ya llegaron
        foreach (GameObject cube in toRemove)
        {
            movingCubes.Remove(cube);
        }
    }

    private void PlaceWinLine(Vector3 pos1, Vector3 pos2, Vector3 pos3)
    {
        
        GameObject winLine = Instantiate(gridLinePrefab);
        LineRenderer lr = winLine.GetComponent<LineRenderer>();
        lr.SetPosition(0, pos1);
        lr.SetPosition(1, pos3);


        // Guardar o animar si deseas
        activeWinLines.Add(winLine); // Si estás guardando estas líneas
        
    }

    private bool CheckWin(char player)
    {
        // Ejes Z (planos XY)
        if (CheckAndDraw(player, 0, 0, 0, 0, 0, 1, 0, 0, 2)) return true;
        if (CheckAndDraw(player, 0, 1, 0, 0, 1, 1, 0, 1, 2)) return true;
        if (CheckAndDraw(player, 0, 2, 0, 0, 2, 1, 0, 2, 2)) return true;
        if (CheckAndDraw(player, 1, 0, 0, 1, 0, 1, 1, 0, 2)) return true;
        if (CheckAndDraw(player, 1, 1, 0, 1, 1, 1, 1, 1, 2)) return true;
        if (CheckAndDraw(player, 1, 2, 0, 1, 2, 1, 1, 2, 2)) return true;
        if (CheckAndDraw(player, 2, 0, 0, 2, 0, 1, 2, 0, 2)) return true;
        if (CheckAndDraw(player, 2, 1, 0, 2, 1, 1, 2, 1, 2)) return true;
        if (CheckAndDraw(player, 2, 2, 0, 2, 2, 1, 2, 2, 2)) return true;

        // Ejes Y (planos XZ)
        if (CheckAndDraw(player, 0, 0, 0, 0, 1, 0, 0, 2, 0)) return true;
        if (CheckAndDraw(player, 0, 0, 1, 0, 1, 1, 0, 2, 1)) return true;
        if (CheckAndDraw(player, 0, 0, 2, 0, 1, 2, 0, 2, 2)) return true;
        if (CheckAndDraw(player, 1, 0, 0, 1, 1, 0, 1, 2, 0)) return true;
        if (CheckAndDraw(player, 1, 0, 1, 1, 1, 1, 1, 2, 1)) return true;
        if (CheckAndDraw(player, 1, 0, 2, 1, 1, 2, 1, 2, 2)) return true;
        if (CheckAndDraw(player, 2, 0, 0, 2, 1, 0, 2, 2, 0)) return true;
        if (CheckAndDraw(player, 2, 0, 1, 2, 1, 1, 2, 2, 1)) return true;
        if (CheckAndDraw(player, 2, 0, 2, 2, 1, 2, 2, 2, 2)) return true;

        // Ejes X (planos YZ)
        if (CheckAndDraw(player, 0, 0, 0, 1, 0, 0, 2, 0, 0)) return true;
        if (CheckAndDraw(player, 0, 0, 1, 1, 0, 1, 2, 0, 1)) return true;
        if (CheckAndDraw(player, 0, 0, 2, 1, 0, 2, 2, 0, 2)) return true;
        if (CheckAndDraw(player, 0, 1, 0, 1, 1, 0, 2, 1, 0)) return true;
        if (CheckAndDraw(player, 0, 1, 1, 1, 1, 1, 2, 1, 1)) return true;
        if (CheckAndDraw(player, 0, 1, 2, 1, 1, 2, 2, 1, 2)) return true;
        if (CheckAndDraw(player, 0, 2, 0, 1, 2, 0, 2, 2, 0)) return true;
        if (CheckAndDraw(player, 0, 2, 1, 1, 2, 1, 2, 2, 1)) return true;
        if (CheckAndDraw(player, 0, 2, 2, 1, 2, 2, 2, 2, 2)) return true;

        // Diagonales espaciales
        if (CheckAndDraw(player, 0, 0, 0, 1, 1, 1, 2, 2, 2)) return true;
        if (CheckAndDraw(player, 0, 2, 0, 1, 1, 1, 2, 0, 2)) return true;
        if (CheckAndDraw(player, 2, 0, 0, 1, 1, 1, 0, 2, 2)) return true;
        if (CheckAndDraw(player, 2, 2, 0, 1, 1, 1, 0, 0, 2)) return true;

        // Plano XY en z
        if (CheckAndDraw(player, 0, 0, 0, 0, 1, 1, 0, 2, 2)) return true; 
        if (CheckAndDraw(player, 0, 0, 2, 0, 1, 1, 0, 2, 0)) return true;
        if (CheckAndDraw(player, 1, 0, 0, 1, 1, 1, 1, 2, 2)) return true;
        if (CheckAndDraw(player, 1, 0, 2, 1, 1, 1, 1, 2, 0)) return true;
        if (CheckAndDraw(player, 2, 0, 0, 2, 1, 1, 2, 2, 2)) return true;
        if (CheckAndDraw(player, 2, 0, 2, 2, 1, 1, 2, 2, 0)) return true;

        // Plano XZ en y
        if (CheckAndDraw(player, 0, 0, 0, 1, 0, 1, 2, 0, 2)) return true;
        if (CheckAndDraw(player, 0, 0, 2, 1, 0, 1, 2, 0, 0)) return true;
        if (CheckAndDraw(player, 0, 1, 0, 1, 1, 1, 2, 1, 2)) return true;
        if (CheckAndDraw(player, 0, 1, 2, 1, 1, 1, 2, 1, 0)) return true;
        if (CheckAndDraw(player, 0, 2, 0, 1, 2, 1, 2, 2, 2)) return true;
        if (CheckAndDraw(player, 0, 2, 2, 1, 2, 1, 2, 2, 0)) return true;

        // Plano YZ en x
        if (CheckAndDraw(player, 0, 0, 0, 1, 1, 0, 2, 2, 0)) return true;
        if (CheckAndDraw(player, 2, 0, 0, 1, 1, 0, 0, 2, 0)) return true;
        if (CheckAndDraw(player, 0, 0, 1, 1, 1, 1, 2, 2, 1)) return true;
        if (CheckAndDraw(player, 2, 0, 1, 1, 1, 1, 0, 2, 1)) return true;
        if (CheckAndDraw(player, 0, 0, 2, 1, 1, 2, 2, 2, 2)) return true;
        if (CheckAndDraw(player, 2, 0, 2, 1, 1, 2, 0, 2, 2)) return true;

        return false;
    }

    private bool CheckAndDraw(char player, int z1, int y1, int x1, int z2, int y2, int x2, int z3, int y3, int x3)
    {
        if (board[z1, y1, x1] == player && board[z2, y2, x2] == player && board[z3, y3, x3] == player)
        {
            Vector3 p1 = boardPositions[z1, y1, x1];
            Vector3 p2 = boardPositions[z2, y2, x2];
            Vector3 p3 = boardPositions[z3, y3, x3];
            PlaceWinLine(p1, p2, p3);
            return true;
        }
        return false;
    }

    private void CheckGameOver()
    {
        if (gameOver && Time.time - gameOverTime >= 5.0f)
        {
            // Reiniciar el juego después de 5 segundos
            ResetGame();
        }
    }

    private void ResetGame()
    {
        // Limpiar tablero
        InitializeBoard();

        // Eliminar todos los cubos
        foreach (GameObject cube in activeCubes)
        {
            Destroy(cube);
        }
        activeCubes.Clear();
        movingCubes.Clear();

        // Reiniciar estado del juego
        gameOver = false;
        winner = 0;
        currentPlayer = 1;
        SendPlayerToESP32();
    }

    private void OnGUI()
    {
        if (gameOver)
        {

        }
    }

    void OnApplicationQuit()
    {
        if (serialPort != null && serialPort.IsOpen)
        {
            serialPort.Close();
        }
    }
    
    void OnDestroy()
    {
        if (bluetoothStream != null && bluetoothStream.IsOpen)
        {
            bluetoothStream.Close();
        }
    }
}