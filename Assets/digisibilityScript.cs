using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using KModkit;
using Rnd = UnityEngine.Random;
using System.Reflection;
using Newtonsoft.Json;
using System.Text.RegularExpressions;

public class digisibilityScript : MonoBehaviour {

	public KMAudio Audio;
	public AudioClip[] Sounds;
	public KMSelectable[] Button;
	public TextMesh[] Text;
	public GameObject Statuslight;
	public KMBombModule Module;
	public KMModSettings ModSettings;

	public class ModSettingsJSON
	{
		public bool enableLegacyVersion;
	}

	private bool enableLegVersion;
	private bool solved;
	private int[][] Data = { new int[] { } };
	private int[] input = { };
	private bool[] selected = new bool[9];
	private int current;

	static int _moduleIdCounter = 1;
	int _moduleID = 0;

	private KMSelectable.OnInteractHandler ButtonPress(int pos)
	{
		return delegate
		{
			Button[pos].AddInteractionPunch();
			if (!solved)
			{
				if (selected[pos])
				{
					Text[0].text = "";
					current = 0;
					for (int i = 0; i < 9; i++)
					{
						if (selected[i])
						{
							Button[i].GetComponent<MeshRenderer>().material.color = new Color32(0, 95, 111, 255);
							Text[i + 2].color = new Color32(63, 191, 223, 255);
						}
					}
					selected = new bool[9];
					Audio.PlaySoundAtTransform("Ping", Module.transform);
				}
				else
				{
					Button[pos].GetComponent<MeshRenderer>().material.color = new Color32(63, 191, 223, 255);
					Text[pos + 2].color = new Color32(0, 95, 111, 255);
					selected[pos] = true;
					current = current * 10 + Data[0][pos];
					Text[0].text = current.ToString();
					if (current.ToString().Length == 9)
					{
						if (Data[1].Contains(current) && !input.Contains(current))
						{
							input = input.Concat(new int[] { current }).ToArray();
							Text[0].text = "";
							selected = new bool[9];
							Text[1].text = input.Length.ToString() + "\nof\n" + Data[1].Length.ToString();
							current = 0;
							Statuslight.GetComponent<MeshRenderer>().material.color = new Color(.25f * ((float)input.Length / ((float)Data[1].Length)), .375f + .375f * ((float)input.Length / ((float)Data[1].Length)), .4375f + .4375f * ((float)input.Length / ((float)Data[1].Length)));
							Audio.PlaySoundAtTransform("Ping", Module.transform);
						}
						else
						{
							Module.HandleStrike();
							Text[0].text = "";
							selected = new bool[9];
							current = 0;
							Audio.PlaySoundAtTransform("Click", Module.transform);
						}
						if (input.Length == Data[1].Length)
						{
							Module.HandlePass();
							Text[0].text = "Good job!";
							for (int i = 2; i < Text.Length; i++)
							{
								Text[i].text = "";
							}
							solved = true;
						}
						else
						{
							for (int i = 0; i < 9; i++)
							{
								Button[i].GetComponent<MeshRenderer>().material.color = new Color32(0, 95, 111, 255);
								Text[i + 2].color = new Color32(63, 191, 223, 255);
							}
						}
					}
					else
					{
						Audio.PlaySoundAtTransform("Click", Module.transform);
					}
				}
			}
			return false;
		};
	}
	void Awake()
	{
		_moduleID = _moduleIdCounter++;
		enableLegVersion = GetLegacySetting();
		for (int i = 0; i < Button.Length; i++)
		{
			Button[i].OnInteract += ButtonPress(i);
		}
	}

	void Start () {
		Data = GenerateSolution();
		Debug.LogFormat("[Digisibility #{0}] Sequence is {1}, resulting in number(s) {2}.", _moduleID, Data[0].Join(""), Data[1].Join(", "));
		Text[0].text = "";
		Text[1].text = "0\nof\n" + Data[1].Length.ToString();
		for (int i = 0; i < 9; i++)
		{
			Text[i + 2].text = Data[0][i].ToString();
		}
	}

	bool GetLegacySetting()
    {
		string missionDesc = KTMissionGetter.Mission.Description;
		if (missionDesc != null)
		{
			Regex regex = new Regex(@"\[Digisibility\] Legacy");
			var match = regex.Match(missionDesc);
			if (match.Success)
				return true;
		}
		string missionId = GetMissionID();
		if (missionId != "undefined" && missionId != "custom")
			return false;
		try
		{
			ModSettingsJSON settings = JsonConvert.DeserializeObject<ModSettingsJSON>(ModSettings.Settings);
			if (settings != null)
				return settings.enableLegacyVersion;
			return false;
		}
		catch (JsonReaderException) {
			return false;
		}
	}

	int[][] GenerateSolution()
	{
		while (true)
		{
			int[] numbers = new int[9];
			for (int i = 0; i < numbers.Length; i++)
			{
				numbers[i] = Rnd.Range(1, 10);
			}
			int[] trials = { };
			for (int i = 1; i < 10; i++)
			{
				if (numbers.Contains(i) && i % numbers[0] == 0)
				{
					trials = trials.Concat(new int[] { i }).ToArray();
				}
			}
			for (int i = 0; i < trials.Length; i++)
			{
				int trial = trials[i];
				int[] tempNums = numbers;
				while (trial != 0)
				{
					bool notdone = true;
					for (int k = 0; k < tempNums.Length && notdone; k++)
					{
						if (tempNums[k] == trial % 10)
						{
							List<int> list = tempNums.ToList();
							list.RemoveAt(k);
							tempNums = list.ToArray();
							notdone = false;
						}
					}
					trial /= 10;
				}
				for (int j = 1; j < 10; j++)
				{
					if (tempNums.Contains(j) && !(trials[i].ToString().Length > 8) && (trials[i] * 10 + j) % numbers[trials[i].ToString().Length] == 0)
					{
						trials = trials.Concat(new int[] { trials[i] * 10 + j }).ToArray();
					}
				}
			}
			if ((enableLegVersion && trials.Count(x => x.ToString().Length == 9) > 0) || (!enableLegVersion && trials.Count(x => x.ToString().Length == 9) == 1))
			{
				return new int[][] { numbers, trials.Where(x => x.ToString().Length == 9).ToArray(), new int[] { } };
			}
		}
	}

	string GetMissionID()
	{
		try
		{
			Component gameplayState = GameObject.Find("GameplayState(Clone)").GetComponent("GameplayState");
			Type type = gameplayState.GetType();
			FieldInfo fieldMission = type.GetField("MissionToLoad", BindingFlags.Public | BindingFlags.Static);
			return fieldMission.GetValue(gameplayState).ToString();
		}
		catch (NullReferenceException)
		{
			return "undefined";
		}
	}

#pragma warning disable 414
	private string TwitchHelpMessage = "'!{0} 381654729' to press those labels. The numbers inputted must be an exact rearrangement of the numbers on the module.";
#pragma warning restore 414
	IEnumerator ProcessTwitchCommand(string command)
	{
		yield return null;
		string valid = "123456789";
		bool[] tempSelect = new bool[9];
		for (int i = 0; i < 9; i++)
		{
			if (command.Length != 9 || !valid.Contains(command[i]))
			{
				yield return "sendtochaterror Invalid command.";
				yield break;
			}
			bool steady = true;
			for (int j = 0; j < 9 && steady; j++)
			{
				if (Data[0][j].ToString() == command[i].ToString() && !tempSelect[j])
				{
					tempSelect[j] = true;
					steady = false;
				}
			}
			if (steady)
			{
				yield return "sendtochaterror Invalid command.";
				yield break;
			}
		}
		for (int i = 0; i < 9; i++)
		{
			bool steady = true;
			for (int j = 0; j < 9 && steady; j++)
			{
				if (command[i].ToString() == Data[0][j].ToString() && !selected[j])
				{
					Button[j].OnInteract();
					steady = false;
					yield return null;
				}
			}
		}
		yield return null;
	}
	IEnumerator TwitchHandleForcedSolve()
	{
		yield return true;
		for (int i = 0; i < Data[1].Length; i++)
		{
			if (!input.Contains(Data[1][i]))
			{
				for (int j = 0; j < 9; j++)
				{
					bool steady = true;
					for (int k = 0; k < 9 && steady; k++)
					{
						if (Data[0][k].ToString() == Data[1][i].ToString()[j].ToString() && !selected[k])
						{
							Button[k].OnInteract();
							steady = false;
							yield return true;
						}
					}
				}
			}
		}
	}
}
