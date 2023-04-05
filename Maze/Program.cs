using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;

namespace Maze;

class Program
{
    static void Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.CursorVisible = false;

        Game game = new Game();

        game.Start();
        game.GameLoop();
    }
}

public enum GenerationFlags
{
    Unexplored,
    Explored,
    Ready,

    Coin,
    Finish,
}

public enum GenerationState
{
    Exploring,
    Revert,
}


public class Game
{
    public Action<int, int, int, int> OnObjectPositionUpdated;
    public Action<int, int> OnMoveInput;


    private int mapSizeX = 159;
    private int mapSizeY = 49;

    private const int maxMapSizeX = 159;
    private const int maxMapSizeY = 49;

    private const int minMapSize = 13;

    private GenerationFlags[,] generationMap = new GenerationFlags[0, 0];
    private GenerationState generationState = GenerationState.Exploring;

    private char[,] map = new char[0, 0];

    private const int collectablesGenerationOffset = 6;


    private bool playerEscaped;

    private int playerX = 1;
    private int playerY = 1;
    
    private int spawnableCoinsAmount = 10;

    public void GameLoop()
    {
        while (true)
        {
            GetInput();

            if (playerEscaped)
                SetupGame();
        }
    }

    public void Start()
    {
        SetupGame();

        OnMoveInput += MovePlayer;
        OnObjectPositionUpdated += UpdateIndividualGraphic;
    }

    private void SetupGame()
    {
        playerX = 1;
        playerY = 1;

        playerEscaped = false;

        MapSetupUI();

        GenerateMaze();
        DrawMap(map);
        UpdateIndividualGraphic(playerX, playerY, playerX, playerY);
    }

    
    private void GenerateMaze()
    {
        InitializeMap();

        Random random = new Random();

        int randomizedStartingPointX = random.Next(3, mapSizeX - 3);
        int randomizedStartingPointY = random.Next(3, mapSizeY - 3);

        int generatingPointX = randomizedStartingPointX % 2 == 0 ? randomizedStartingPointX - 1 : randomizedStartingPointX;
        int generatingPointY = randomizedStartingPointY % 2 == 0 ? randomizedStartingPointY - 1 : randomizedStartingPointY;

        int previousGeneratingPointX = generatingPointX;
        int previousGeneratingPointY = generatingPointY;

        int[] generatingPointAvailableWayPoints = new int[1];
        Stack<(int x, int y)> generationExploredPath = new Stack<(int x, int y)>();


        do
        {
            switch (CheckGeneratingPointNeighbours(generatingPointX, generatingPointY))
            {
                case GenerationFlags.Unexplored:
                    generationState = GenerationState.Exploring;

                    generatingPointAvailableWayPoints = GetAvailableWayPoints(generatingPointX, generatingPointY);

                    switch (generatingPointAvailableWayPoints[random.Next(0, generatingPointAvailableWayPoints.Length)])
                    {
                        case 0: // left
                            generatingPointX -= 2;
                            break;
                        case 1: // right
                            generatingPointX += 2;
                            break;

                        case 2: // up
                            generatingPointY -= 2;
                            break;
                        case 3: // down
                            generatingPointY += 2;
                            break;
                    }

                    if (!CheckGeneratingPointOutOfBounds(generatingPointX, generatingPointY))
                    {
                        generatingPointX = previousGeneratingPointX;
                        generatingPointY = previousGeneratingPointY;
                        continue;
                    }


                    FlagPointsBetween(previousGeneratingPointX, previousGeneratingPointY,
                        generatingPointX, generatingPointY, GenerationFlags.Explored);

                    generationExploredPath.Push((generatingPointX, generatingPointY));
                    previousGeneratingPointX = generatingPointX;
                    previousGeneratingPointY = generatingPointY;
                    break;


                case GenerationFlags.Explored:
                    generationState = GenerationState.Revert;

                    (generatingPointX, generatingPointY) = generationExploredPath.Pop();
                    previousGeneratingPointX = generatingPointX;
                    previousGeneratingPointY = generatingPointY;

                    FlagPointsBetween(previousGeneratingPointX, previousGeneratingPointY,
                        generatingPointX, generatingPointY, GenerationFlags.Ready);
                    break;
            }
        }
        while (generationExploredPath.Count != 0);

        GeneratePickables();
        generationMap[mapSizeX - 2, mapSizeY - 2] = GenerationFlags.Finish; 
        BuildGraphicsMap();
    }

    private bool CheckGeneratingPointOutOfBounds(int pointX, int pointY)
    {
        if (pointX == -1 || pointY == -1)
            return false;

        if (pointX >= generationMap.GetLength(0) || pointY == generationMap.GetLength(1))
            return false;

        return true;
    }

    private int[] GetAvailableWayPoints(int pointX, int pointY)
    {
        GenerationFlags[] neighbourFlags = GetGeneratingPointNeighbours(pointX, pointY);
        List<int> availableWayPoints = new List<int>();

        for (int i = 0; i < 4; i++)
        {
            if (generationState == GenerationState.Exploring && neighbourFlags[i] == GenerationFlags.Unexplored)
                availableWayPoints.Add(i);

            if (generationState == GenerationState.Revert && neighbourFlags[i] == GenerationFlags.Explored)
                availableWayPoints.Add(i);
        }

        return availableWayPoints.ToArray();
    }

    private GenerationFlags CheckGeneratingPointNeighbours(int pointX, int pointY)
    {
        GenerationFlags[] neighbourFlags = GetGeneratingPointNeighbours(pointX, pointY);

        for (int i = 0; i < 4; i++)
        {
            if (neighbourFlags[i] == GenerationFlags.Unexplored)
                return GenerationFlags.Unexplored;
        }

        return GenerationFlags.Explored;
    }

    private GenerationFlags[] GetGeneratingPointNeighbours(int pointX, int pointY)
    {
        GenerationFlags[] neighbourFlags = new GenerationFlags[4];

        int left = Math.Clamp(pointX - 2, 1, mapSizeX - 2);
        int right = Math.Clamp(pointX + 2, 1, mapSizeX - 2);
        int up = Math.Clamp(pointY - 2, 1, mapSizeY - 2);
        int down = Math.Clamp(pointY + 2, 1, mapSizeY - 2);

        neighbourFlags[0] = generationMap[left, pointY];
        neighbourFlags[1] = generationMap[right, pointY];
        neighbourFlags[2] = generationMap[pointX, up];
        neighbourFlags[3] = generationMap[pointX, down];

        return neighbourFlags;
    }

    private void FlagPointsBetween(int fromPointX, int fromPointY, int toPointX, int toPointY, GenerationFlags flag)
    {
        int midPointX = (fromPointX + toPointX) >> 1;
        int midPointY = (fromPointY + toPointY) >> 1;

        generationMap[fromPointX, fromPointY] = flag;
        generationMap[midPointX, midPointY] = flag;
        generationMap[toPointX, toPointY] = flag;
    }

    private void GeneratePickables()
    {
        Random random = new Random();

        int spawnX = 0;
        int spawnY = 0;

        for (int i = 0; i < spawnableCoinsAmount; i++)
        {
            spawnX = random.Next(collectablesGenerationOffset, mapSizeX - collectablesGenerationOffset);
            spawnY = random.Next(collectablesGenerationOffset, mapSizeY - collectablesGenerationOffset);

            generationMap[spawnX, spawnY] = GenerationFlags.Coin;
        }
    }

    private void InitializeMap()
    {
        generationMap = new GenerationFlags[mapSizeX, mapSizeY];
        map = new char[mapSizeX, mapSizeY];

        for (int y = 0; y < mapSizeY; y++)
        {
            for (int x = 0; x < mapSizeX; x++)
            {
                generationMap[x, y] = GenerationFlags.Unexplored;
            }
        }
    }

    private void BuildGraphicsMap()
    {
        for (int y = 0; y < mapSizeY; y++)
        {
            for (int x = 0; x < mapSizeX; x++)
            {
                switch (generationMap[x, y]) 
                { 
                    case GenerationFlags.Ready:
                        map[x, y] = ' ';
                        break;

                    case GenerationFlags.Explored:
                        map[x, y] = ' ';
                        break;

                    case GenerationFlags.Unexplored:
                        map[x, y] = '#';
                        break;

                    case GenerationFlags.Finish:
                        map[x, y] = '^';
                        break;

                    case GenerationFlags.Coin:
                        map[x, y] = '●';
                        break;
                }
            }
        }
    }



    private void MovePlayer(int moveX, int moveY)
    {
        if (!CheckCollision(moveX, moveY))
            return;


        int newPositionX = Math.Clamp(playerX + moveX, 0, map.Length - 1);
        int newPositionY = Math.Clamp(playerY + moveY, 0, map.Length - 1);

        OnObjectPositionUpdated.Invoke(playerX, playerY, newPositionX, newPositionY);

        playerX = newPositionX;
        playerY = newPositionY;
    }

    private bool CheckCollision(int moveX, int moveY)
    {
        switch (GetNextMoveBlock(moveX, moveY))
        {
            case '#':
                return false;

            case '^':
                playerEscaped = true;
                return true;
        }

        return true;
    }

    private char GetNextMoveBlock(int moveX, int moveY)
    {
        return map[playerX + moveX, playerY + moveY];
    }


    public void GetInput()
    {
        if (!Console.KeyAvailable)
            return;

        ConsoleKey keyInput = Console.ReadKey(true).Key;

        int deltaX = keyInput == ConsoleKey.W ? -1 :
                     keyInput == ConsoleKey.S ? 1 : 0;

        int deltaY = keyInput == ConsoleKey.A ? -1 :
                     keyInput == ConsoleKey.D ? 1 : 0;


        OnMoveInput.Invoke(deltaY, deltaX);
    }



    private void MapSetupUI() //Я НЕ ЗНАЮ ЯК ТО ПРАВИЛЬНО РОБИТИ
    {
        Console.Clear();

        bool settedUp = false;

        while (!settedUp)
        {
            Console.SetCursorPosition(0, 0);
            Console.WriteLine("Enter the map size:");
            Console.WriteLine("Width:                                                        ");
            Console.Write("Height:                                                           ");

            Console.SetCursorPosition(0, 1);
            Console.Write("Width: ");
            try
            {
                mapSizeX = int.Parse(Console.ReadLine());
            }
            catch
            {
                Console.SetCursorPosition(0, 4);
                Console.Write("Wrong input!");
                continue;
            }
            if (mapSizeX % 2 == 0)
                mapSizeX--;

            mapSizeX = Math.Clamp(mapSizeX, minMapSize, maxMapSizeX);


            Console.SetCursorPosition(0, 2);
            Console.Write("Height: ");
            try
            {
                mapSizeY = int.Parse(Console.ReadLine());
            }
            catch
            {
                Console.SetCursorPosition(0, 4);
                Console.Write("Wrong input!");
                continue;
            }
            if (mapSizeY % 2 == 0)
                mapSizeY--;

            mapSizeY = Math.Clamp(mapSizeY, minMapSize, maxMapSizeY);

            try
            {
                Console.SetWindowSize(mapSizeX, mapSizeY);
            }
            catch
            {
                Console.SetCursorPosition(0, 4);
                Console.Write("This map is too big for the size of the window!");
                continue;
            }

            settedUp = true;
        }

        Console.Clear();
    }



    private void UpdateIndividualGraphic(int oldX, int oldY, int newX, int newY)
    {
        Console.SetCursorPosition(oldX, oldY);
        Console.Write(' ');

        Console.SetCursorPosition(newX, newY);
        Console.Write('v');
    }


    private void DrawMap(char[,] map)
    {
        Console.SetCursorPosition(0, 0);

        for (int y = 0; y < mapSizeY; y++)
        {
            for (int x = 0; x < mapSizeX; x++)
            {
                Console.ForegroundColor = GetBlockColor(map[x, y]);
                Console.Write(map[x, y]);

                Console.ForegroundColor = ConsoleColor.White;
            }
            Console.Write('\n');
        }

        Console.SetCursorPosition(0, 0);
    }

    private ConsoleColor GetBlockColor(char block)
    {
        switch (block)
        {
            case '#':
                return ConsoleColor.DarkGray;

            case '^':
                return ConsoleColor.Green;

            case '●':
                return ConsoleColor.Yellow;
        }

        return ConsoleColor.White;
    }
}