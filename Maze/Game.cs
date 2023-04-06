using System;
using System.Text;

namespace Maze;

public class Game
{
    int mapSizeX = 159;
    private int mapSizeY = 49;

    private const int maxMapSizeX = 159;
    private const int maxMapSizeY = 159;

    private const int minMapSize = 9;

    private GenerationFlags[,] generationMap = new GenerationFlags[0, 0];
    private GenerationState generationState = GenerationState.Exploring;

    private char[,] map = new char[0, 0];

    private const int collectablesGenerationOffset = 1;


    private bool playerEscaped;
    private bool playerSurrendered;

    private int playerX = 1;
    private int playerY = 1;

    private int collectedCoins = 0;
    private int spawnableCoinsAmount = 10;


    private int escapeX = 0;
    private int escapeY = 0;


    private ConsoleKey? latestInput;

    public void GameLoop()
    {
        while (true)
        {
            GetInput();
            CheckInput();

            EscapeUpdate();
            MovePlayer();

            if (playerEscaped || playerSurrendered)
                SetupGame();

            DrawMap();
        }
    }

    public void Start()
    {
        SetupGame();
    }

    private void SetupGame()
    {
        MainMenuUI();

        GenerateMaze();
        SetupPlayer();

        DrawMap();
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

        escapeX = mapSizeX - 2;
        escapeY = mapSizeY - 2;
        generationMap[escapeX, escapeY] = GenerationFlags.Escape;

        GenerateCoins();

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

    private void GenerateCoins()
    {
        Random random = new Random();

        int spawnX = 0;
        int spawnY = 0;

        for (int i = 0; i < spawnableCoinsAmount; i++)
        {
            spawnX = random.Next(collectablesGenerationOffset, mapSizeX - collectablesGenerationOffset);
            spawnY = random.Next(collectablesGenerationOffset, mapSizeY - collectablesGenerationOffset);

            if (generationMap[spawnX, spawnY] is GenerationFlags.Coin or 
                                                 GenerationFlags.Unexplored or 
                                                 GenerationFlags.Escape)
            {
                i--;
                continue;
            }

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

                    case GenerationFlags.Escape:
                        map[x, y] = 'x';
                        break;

                    case GenerationFlags.Coin:
                        map[x, y] = '*';
                        break;
                }
            }
        }
    }



    private void SetupPlayer()
    {
        playerX = 1;
        playerY = 1;

        collectedCoins = 0;

        playerEscaped = false;
        playerSurrendered = false;
    }

    private void MovePlayer()
    {
        if (latestInput == null)
            return;

        (int deltaX, int deltaY) = ProcessMovementInput();

        if (!ProcessCollision(deltaX, deltaY))
            return;

        int newPositionX = Math.Clamp(playerX + deltaX, 0, map.Length - 1);
        int newPositionY = Math.Clamp(playerY + deltaY, 0, map.Length - 1);

        playerX = newPositionX;
        playerY = newPositionY;
    }

    private bool ProcessCollision(int moveX, int moveY)
    {
        int newPlayerX = playerX + moveX;
        int newPlayerY = playerY + moveY;

        switch (GetNextMoveBlock(moveX, moveY))
        {
            case '#':
                return false;

            case '^':
                playerEscaped = true;
                return true;

            case '*':
                map[newPlayerX, newPlayerY] = ' ';
                collectedCoins++;
                return true;
        }

        return true;
    }

    private (int deltaX, int deltaY) ProcessMovementInput()
    {
        int deltaX = latestInput == ConsoleKey.A ? -1 :
                     latestInput == ConsoleKey.D ? 1 : 0;

        int deltaY = latestInput == ConsoleKey.W ? -1 :
                     latestInput == ConsoleKey.S ? 1 : 0;

        return (deltaX, deltaY);
    }

    private char GetNextMoveBlock(int moveX, int moveY)
    {
        return map[playerX + moveX, playerY + moveY];
    }

    private void CheckInput()
    {
        switch (latestInput)
        {
            case ConsoleKey.T:
                playerSurrendered = true;
                return;
        }
    }


    private void GetInput()
    {
        if (latestInput != null)
            latestInput = null;

        if (!Console.KeyAvailable)
            return;

        latestInput = Console.ReadKey(true).Key;
    }


    private void EscapeUpdate()
    {
        if (collectedCoins < 10)
        {
            map[escapeX, escapeY] = 'x';
            return;
        }

        map[escapeX, escapeY] = '^';
    }


    private void InGameUI()
    {
        Console.SetCursorPosition(0, mapSizeY);

        Console.Write($"{SetColor(255, 255, 255)}Collected Coins: {SetColor(255, 255, 0)}{collectedCoins} {SetColor(255, 255, 255)}");
    }

    private void MainMenuUI() //Я НЕ ЗНАЮ ЯК ТО ПРАВИЛЬНО РОБИТИ
    {
        Console.Clear();
        Console.SetWindowSize(50, 17);

        while (true)
        {
            Console.SetCursorPosition(0, 0);
            Console.WriteLine("Enter the map size:");
            Console.WriteLine("Width:                                                        ");
            Console.Write("Height:                                                           \n\n\n\n");

            Console.WriteLine($"{SetColor(255, 255, 0)}* {SetColor(255, 255, 255)}Coin");
            Console.WriteLine($"{SetColor(255, 0, 0)}x {SetColor(255, 255, 255)}Escape closed");
            Console.WriteLine($"{SetColor(0, 255, 0)}^ {SetColor(255, 255, 255)}Escape");
            Console.WriteLine($"{SetColor(69, 69, 69)}# {SetColor(255, 255, 255)}Wall \n");

            Console.WriteLine($"{SetColor(255, 255, 255)}Press {SetColor(0, 255, 0)}T {SetColor(255, 255, 255)}To surrender \n");

            if (playerEscaped)
                Console.WriteLine($"{SetColor(0, 255, 0)}Congratulations! You escaped the maze! {SetColor(255, 255, 255)}");

            else if (playerSurrendered)
                Console.WriteLine($"{SetColor(255, 0, 0)}You have surrendered... {SetColor(255, 255, 255)}");

            Console.Write($"\n\nTo {SetColor(0, 255, 0)}escape{SetColor(255, 255, 255)} you have to get all {SetColor(255, 255, 0)}10 coins{SetColor(255, 255, 255)}");


            Console.SetCursorPosition(0, 1);
            Console.Write("Width: ");
            try
            {
                mapSizeX = int.Parse(Console.ReadLine());
            }
            catch
            {
                Console.SetCursorPosition(0, 4);
                Console.Write($"{SetColor(255, 0, 0)}Wrong input!{SetColor(255, 255, 255)}                                                            ");
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
                Console.Write($"{SetColor(255, 0, 0)}Wrong input!{SetColor(255, 255, 255)}                                                            ");
                continue;
            }
            if (mapSizeY % 2 == 0)
                mapSizeY--;

            mapSizeY = Math.Clamp(mapSizeY, minMapSize, maxMapSizeY);

            try
            {
                Console.SetWindowSize(mapSizeX + 12, mapSizeY + 1);
            }
            catch
            {
                Console.SetCursorPosition(0, 4);
                Console.Write($"{SetColor(255, 0, 0)}This map is too big for the size of the window!{SetColor(255, 255, 255)}");
                continue;
            }

            break;
        }

        Console.Clear();
    }

    private void DrawMap()
    {
        StringBuilder screenBuffer = new StringBuilder();

        for (int y = 0; y < mapSizeY; y++)
        {
            for (int x = 0; x < mapSizeX; x++)
            {
                screenBuffer.Append(GetBlock(x, y));
            }
            screenBuffer.Append('\n');
        }

        Console.SetCursorPosition(0, 0);
        Console.Write(screenBuffer);

        InGameUI();
    }

    private string GetBlock(int x, int y)
    {
        if (playerX == x && playerY == y)
            return SetColor(255, 255, 255) + 'v';

        switch (map[x, y])
        {
            case '#':
                return SetColor(69, 69, 69) + '#';

            case '^':
                return SetColor(0, 255, 0) + '^';

            case 'x':
                return SetColor(255, 0, 0) + 'x';

            case '*':
                return SetColor(255, 255, 0) + '*';

            case ' ':
                return " ";
        }

        return SetColor(255, 255, 255) + 'n';
    }

    private string SetColor(byte r, byte g, byte b)
        => $"\x1b[38;2;{r};{g};{b}m";
}
