using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using KModkit;
using rnd = UnityEngine.Random;

public class codenames : MonoBehaviour
{
    public KMAudio Audio;
    public KMBombInfo bomb;
    public KMColorblindMode Colorblind;
    private bool colorblindActive = false;
    public GameObject cblindback;
    public GameObject cblindtext;

    public TextMesh mainword;
    public KMSelectable mainbutton;
    public KMSelectable togglebutton;
    public Renderer modulebackground;
    public Material[] teamcolors;
    public Color[] wordcolors;
    public Color[] modulecolors;

    private int posix;
    private int teamindex;
    private int cardindex;
    private int rotationindex;
    private int ruleindex;
    private string[] grid = new string[25];
    private bool[] solution = new bool[25];
    private bool[] pressed = new bool[25];
    private bool isCycling = true;

    private static readonly string[] rotationnames = new string[4] { "not rotated", "rotated 90 degrees clock wise", "rotated 180 degrees", "rotated 90 degrees counterclockwise" };
    private static readonly string[] colornames = new string[2] { "red", "blue" };
    private static readonly string[] soundnames = new string[6] { "card1", "card2", "card3", "card4", "card5", "gunshot" };

    static int moduleIdCounter = 1;
    private Coroutine cycler;
    int moduleId;
    private bool moduleSolved;
    private bool assassinated;
    private bool detonating;

    void Awake()
    {
      moduleId = moduleIdCounter++;
      colorblindActive = Colorblind.ColorblindModeActive;
      cblindback.GetComponent<TextMesh>().text = "";
      cblindtext.GetComponent<TextMesh>().text = "";
      togglebutton.OnInteract += delegate () { toggleCycling(); return false; };
      mainbutton.OnInteract += delegate () { submit(); return false; };
      bomb.OnBombExploded += delegate() { assassinated = true; };
      GetComponent<KMBombModule>().OnActivate += OnActivate;
    }

    void Start()
    {
      posix = rnd.Range(0,25);
      if (bomb.GetSerialNumberNumbers().Last() % 2 == 0)
        ruleindex = 0;
      else if (bomb.GetBatteryHolderCount() % 2 == 0)
        ruleindex = 1;
      else
        ruleindex = 2;
      reset();
    }

    void OnActivate()
    {
        Debug.LogFormat("[Codenames #{0}] Colorblind mode: {1}", moduleId, colorblindActive);
        if (colorblindActive)
        {
            if(colornames[teamindex - 1].Equals("blue"))
                cblindback.GetComponent<TextMesh>().text = "Blue";
            else
                cblindback.GetComponent<TextMesh>().text = "Red";
        }
    }

    void reset()
    {
      var attempts = 0;
      tryagain:
      teamindex = rnd.Range(1,3);
      cardindex = rnd.Range(0,5);
      rotationindex = rnd.Range(0,4);
      for (int i = 0; i < 25; i++)
      {
        grid[i] = Words.possiblewords[rnd.Range(0,5)][rnd.Range(0,3)][rnd.Range(0,15)];
        if (i != 0)
          while (grid.Take(i - 1).Any(x => x == grid[i]))
            grid[i] = Words.possiblewords[rnd.Range(0,5)][rnd.Range(0,3)][rnd.Range(0,15)];
      }
      for (int i = 0; i < 25; i++)
        solution[i] = (Words.possiblewords[cardindex][ruleindex].Contains(grid[i]) && Cards.possiblecards[cardindex][rotationindex][i] == teamindex);
      if (solution.Count(b => b) == 0)
      {
        attempts++;
        goto tryagain;
      }
      modulebackground.material.color = modulecolors[teamindex - 1];
      Debug.LogFormat("[Codenames #{0}] Found a solution in {1} tries.", moduleId, attempts + 1);
      Debug.LogFormat("[Codenames #{0}] You are on the {1} team.", moduleId, colornames[teamindex - 1]);
      Debug.LogFormat("[Codenames #{0}] The card present is card {1}, which is {2}.", moduleId, cardindex + 1, rotationnames[rotationindex]);
      foreach (string word in grid)
        if (solution[Array.IndexOf(grid, word)])
          Debug.LogFormat("[Codenames #{0}] You need to submit {1}.", moduleId, word);
      cycler = StartCoroutine(cycleWords());
    }

    private IEnumerator cycleWords()
    {
      while (true)
      {
        mainword.text = grid[posix];
        mainword.color = ((Cards.possiblecards[cardindex][rotationindex][posix] == 1 || Cards.possiblecards[cardindex][rotationindex][posix] == 2) ? wordcolors[0] : wordcolors[1]);
        if (colorblindActive)
        {
            if(mainword.color == wordcolors[1])
                cblindtext.GetComponent<TextMesh>().text = "Black";
            else
                cblindtext.GetComponent<TextMesh>().text = "Pink";
        }
        if(speed == true)
           yield return new WaitForSeconds(0.2f);
        else
           yield return new WaitForSeconds(1f);
        posix = (posix + 1) % 25;
      }
    }

    void submit()
    {
      var ix = posix;
      if (!moduleSolved && !detonating && !pressed[ix])
      {
        if (Cards.possiblecards[cardindex][rotationindex][ix] == 3)
        {
          Debug.LogFormat("[Codenames #{0}] You submitted the assassin.", moduleId);
          StartCoroutine(solve("Big Mistake."));
        }
        else if (!solution[ix])
        {
          GetComponent<KMBombModule>().HandleStrike();
          Debug.LogFormat("[Codenames #{0}] You submitted {1}. That was incorrect. Strike!", moduleId, grid[ix]);
        }
        else
        {
          Debug.LogFormat("[Codenames #{0}] You submitted {1}.", moduleId, grid[ix]);
          Audio.PlaySoundAtTransform(soundnames[rnd.Range(0,5)], mainbutton.transform);
          pressed[ix] = true;
        }
        if (pressed.SequenceEqual(solution))
        {
          Debug.LogFormat("[Codenames #{0}] Module solved.", moduleId);
          moduleSolved = true;
          cblindback.GetComponent<TextMesh>().text = "";
          cblindtext.GetComponent<TextMesh>().text = "";
          StartCoroutine(solve("Solved!"));
        }
      }
      else
        return;
    }

    private IEnumerator solve(string nextmessage)
    {
      StopCoroutine(cycler);
      var currentmessage = mainword.text;
      var messagelength = currentmessage.Length;
      for (int i = 0; i < messagelength; i++)
      {
        currentmessage = currentmessage.Remove(currentmessage.Length - 1);
        mainword.text = currentmessage;
        yield return new WaitForSeconds(.2f);
      }
      yield return new WaitForSeconds(.3f);
      currentmessage = "";
      mainword.color = wordcolors[moduleSolved ? 2 : 3];
      for (int i = 0; i < nextmessage.Length; i++)
      {
        currentmessage = currentmessage + nextmessage[i];
        mainword.text = currentmessage;
        yield return new WaitForSeconds(.2f);
      }
      if (moduleSolved)
      {
        GetComponent<KMBombModule>().HandlePass();
        Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.CorrectChime, transform);
      }
      else
      {
        detonating = true;
        Audio.PlaySoundAtTransform(soundnames[5], mainbutton.transform);
        yield return new WaitForSeconds(4.5f);
        while (!assassinated)
        {
          GetComponent<KMBombModule>().HandleStrike();
          yield return new WaitForSeconds(.2f);
        }
      }
    }

    void toggleCycling()
    {
      togglebutton.AddInteractionPunch(.5f);
      Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, togglebutton.transform);
      if (isCycling)
      {
        StopCoroutine(cycler);
        isCycling = false;
      }
      else
      {
        cycler = StartCoroutine(cycleWords());
        isCycling = true;
      }
    }

    //twitch plays
    private bool speed = false;

    private IEnumerator cancelSpeed()
    {
        yield return new WaitForSeconds(5.0f);
        speed = false;
    }

    private bool aboutToSolve(int index)
    {
        int counter = 0;
        int counter2 = 0;
        bool correct = false;
        for(int i = 0; i < solution.Length; i++)
          if(solution[i] == true)
            counter++;
        for (int i = 0; i < pressed.Length; i++)
          if (pressed[i] == true)
            counter2++;
        if(solution[index] == true && pressed[index] == false)
          correct = true;
        if((counter2 == (counter - 1)) && correct)
          return true;
        return false;
    }

    #pragma warning disable 414
    private readonly string TwitchHelpMessage = @"!{0} submit <word> [Submits the card with the specified word] | !{0} colorblind [Toggles colorblind mode]";
    #pragma warning restore 414
    IEnumerator ProcessTwitchCommand(string command)
    {
        if (Regex.IsMatch(command, @"^\s*colorblind\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            yield return null;
            Debug.LogFormat("[Codenames #{0}] Colorblind mode toggled! (TP)", moduleId);
            if (colorblindActive)
            {
                colorblindActive = false;
                cblindback.GetComponent<TextMesh>().text = "";
                cblindtext.GetComponent<TextMesh>().text = "";
            }
            else
            {
                colorblindActive = true;
                if (colornames[teamindex - 1].Equals("blue"))
                    cblindback.GetComponent<TextMesh>().text = "Blue";
                else
                    cblindback.GetComponent<TextMesh>().text = "Red";
            }
            yield break;
        }
        string[] parameters = command.Split(' ');
        if (Regex.IsMatch(parameters[0], @"^\s*submit\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            if(parameters.Length >= 2)
            {
                string temp = "";
                int index = -1;
                for(int i = 1; i < parameters.Length; i++)
                {
                    if(i == 1)
                        temp += parameters[i];
                    else
                        temp += " " + parameters[i];
                }
                for (int i = 0; i < grid.Length; i++)
                    if (grid[i].EqualsIgnoreCase(temp))
                        index = i;
                if(index == -1)
                {
                    yield return "sendtochaterror The specified word '"+temp+"' is not on any of the cards!";
                    yield break;
                }
                yield return null;
                if(solution[index] == false)
                    yield return "strike";
                else if(aboutToSolve(index))
                    yield return "solve";
                speed = true;
                StartCoroutine(cancelSpeed());
                while (!mainword.text.EqualsIgnoreCase(temp))
                {
                    yield return "trycancel Card submission halted due to a request to cancel!";
                    yield return new WaitForSeconds(0.1f);
                }
                speed = false;
                mainbutton.OnInteract();
            }
            else
                yield return "sendtochaterror Please include a word to submit!";
            yield break;
        }
    }

    IEnumerator TwitchHandleForcedSolve()
    {
        speed = true;
        for(int i = 0; i < solution.Length; i++)
        {
            if(solution[i] == true)
            {
                while (!grid[i].EqualsIgnoreCase(mainword.text))
                    yield return new WaitForSeconds(0.1f);
                mainbutton.OnInteract();
            }
        }
        yield return true;
    }
}
