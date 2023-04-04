using System;
using System.Net.Http.Headers;

namespace Maze;

class Program
{
    static void Main(string[] args)
    {
        
        Console.CursorVisible = false;

        Game game = new Game();

        game.GameLoop();

        
    }
}

public enum GenerationFlags
{
    Unexplored,
    Explored,
    Ready,

    Coin,
    Key,
    Finish,
}

public enum GenerationState
{
    Exploring,
    Revert,
}


public class Game
{
    public Action<int, int, int, int> OnPlayerPositionUpdated;
    public Action<int, int> OnPlayerMove;


    int mapSizeX = 43;
    int mapSizeY = 23;

    private GenerationFlags[,] generationMap;
    private Stack<(int x, int y)> generationExploredPath = new Stack<(int x, int y)>();
    private GenerationState generationState = GenerationState.Exploring;

    private char[,] map = new char[1,1];



    private bool playerEscaped;

    private int playerX = 1;
    private int playerY = 1;

    private const char playerSprite = 'v';

    public void GameLoop()
    {
        GenerateMaze();
        Console.SetWindowSize(mapSizeX, mapSizeY);

        DrawMap(map);
        UpdateIndividualGraphic(playerX, playerY, playerX, playerY);

        OnPlayerMove += MovePlayer;
        OnPlayerPositionUpdated += UpdateIndividualGraphic;


        while (!playerEscaped)
        {
            GetInput();
        }
    }


    
    private void GenerateMaze()
    {
        bool mazeGenerated = false;

        int generatingPointX = 1;
        int generatingPointY = 1;

        int previousGeneratingPointX = 1;
        int previousGeneratingPointY = 1;

        int[] generatingPointAvailableWayPoints = new int[1];

        Random random = new Random();

        InitializeMap();

        while (!mazeGenerated)
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
                    if (generationExploredPath.Count == 0)
                    {
                        mazeGenerated = true;
                        break;
                    }    

                    generationState = GenerationState.Revert;
                    
                    (generatingPointX, generatingPointY) = generationExploredPath.Pop();
                    previousGeneratingPointX = generatingPointX;
                    previousGeneratingPointY = generatingPointY;

                    FlagPointsBetween(previousGeneratingPointX, previousGeneratingPointY,
                        generatingPointX, generatingPointY, GenerationFlags.Ready);
                    break;
            }

            BuildGraphicsMap();
            DrawMap(map);
        }

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
        bool hasExplored = false;

        for (int i = 0; i < 4; i++)
        {
            if (neighbourFlags[i] == GenerationFlags.Unexplored)
                return GenerationFlags.Unexplored;

            if (neighbourFlags[i] == GenerationFlags.Explored)
                hasExplored = true;
        }

        if (hasExplored)
            return GenerationFlags.Explored;

        return GenerationFlags.Explored;
    }

    private GenerationFlags[] GetGeneratingPointNeighbours(int pointX, int pointY)
    {
        GenerationFlags[] neighbourFlags = new GenerationFlags[4];

        int left = Math.Clamp(pointX - 2, 1, generationMap.GetLength(0) - 2);
        int right = Math.Clamp(pointX + 2, 1, generationMap.GetLength(0) - 2);
        int up = Math.Clamp(pointY - 2, 1, generationMap.GetLength(1) - 2);
        int down = Math.Clamp(pointY + 2, 1, generationMap.GetLength(1) - 2);

        neighbourFlags[0] = generationMap[left, pointY];
        neighbourFlags[1] = generationMap[right, pointY];
        neighbourFlags[2] = generationMap[pointX, up];
        neighbourFlags[3] = generationMap[pointX, down];

        return neighbourFlags;
    }

    private void FlagPointsBetween(int fromPointX, int fromPointY, int toPointX, int toPointY, GenerationFlags flag)
    {
        int midPointX = (fromPointX + toPointX) / 2;
        int midPointY = (fromPointY + toPointY) / 2;

        generationMap[fromPointX, fromPointY] = flag;
        generationMap[midPointX, midPointY] = flag;
        generationMap[toPointX, toPointY] = flag;
    }

    private void FlagPoint(int pointX, int pointY, GenerationFlags flag)
    {
        generationMap[pointX, pointY] = flag;
    }

    private void BuildGraphicsMap()
    {
        for (int y = 0; y < generationMap.GetLength(1); y++)
        {
            for (int x = 0; x < generationMap.GetLength(0); x++)
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
                }
            }
        }
    }

    private void InitializeMap()
    {
        generationMap = new GenerationFlags[mapSizeX, mapSizeY];
        map = new char[mapSizeX, mapSizeY];

        for (int y = 0; y < generationMap.GetLength(1); y++)
        {
            for (int x = 0; x < generationMap.GetLength(0); x++)
            {
                generationMap[x, y] = GenerationFlags.Unexplored;
            }
        }
    }



    private void MovePlayer(int moveX, int moveY)
    {
        switch (GetNextMoveBlock(moveX, moveY))
        {
            case '#':
                return;

            case '^':
                playerEscaped = true;
                return;
        }

        int newPositionX = Math.Clamp(playerX + moveX, 0, map.Length - 1);
        int newPositionY = Math.Clamp(playerY + moveY, 0, map.Length - 1);

        OnPlayerPositionUpdated.Invoke(playerX, playerY, newPositionX, newPositionY);

        playerX = newPositionX;
        playerY = newPositionY;
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


        OnPlayerMove.Invoke(deltaY, deltaX);
    }



    private void UpdateIndividualGraphic(int oldX, int oldY, int newX, int newY)
    {
        Console.SetCursorPosition(oldX, oldY);
        Console.Write(' ');

        Console.SetCursorPosition(newX, newY);
        Console.Write('v');
    }


    public void DrawMap(char[,] map)
    {
        for (int y = 0; y < map.GetLength(1); y++)
        {
            for (int x = 0; x < map.GetLength(0); x++)
            {
                Console.SetCursorPosition(x, y);
                
                Console.Write(map[x, y]);
            }
        }
    }
}