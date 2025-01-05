using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using System;
using System.IO;
using System.Reflection;
using UnityEngine;
using LethalLib.Modules;
using System.Security.Permissions;
using Shrimp.Patches;
using HarmonyLib.Tools;
using Unity.Netcode;
using System.Collections.Generic;


namespace Shrimp
{
    [BepInPlugin(modGUID, modName, modVersion)]
    public class Plugin : BaseUnityPlugin
    {
        private const string modGUID = "Piggy.Shrimp";
        private const string modName = "Shrimp";
        private const string modVersion = "1.0.0";

        private readonly Harmony harmony = new Harmony(modGUID);

        private static Plugin Instance;

        public static ManualLogSource mls;
        public static AssetBundle Bundle;

        public static AudioClip[] footsteps;
        public static AudioClip footstep1;
        public static AudioClip footstep2;
        public static AudioClip footstep3;
        public static AudioClip footstep4;
        public static AudioClip dogEatItem;
        public static AudioClip dogEatPlayer;
        public static AudioClip bigGrowl;
        public static AudioClip enragedScream;
        public static AudioClip dogSprint;
        public static AudioClip ripPlayerApart;
        public static AudioClip cry1;
        public static AudioClip dogHowl;
        public static AudioClip stomachGrowl;

        public static AudioClip eatenExplode;
        public static AudioClip dogSneeze;
        public static AudioClip dogSatisfied;

        public static GameObject shrimpPrefab;
        public static EnemyType shrimpEnemy;

        public static GameObject shrimpItemManager;

        public static TerminalNode shrimpTerminalNode;
        public static TerminalKeyword shrimpTerminalKeyword;

        public static string PluginDirectory;

        private ConfigEntry<int> shrimpSpawnWeight;
        //private ConfigEntry<int> shrimpModdedSpawnWeight;

        public static bool setKorean;

        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }

            PluginDirectory = base.Info.Location;

            mls = BepInEx.Logging.Logger.CreateLogSource(modGUID);

            try
            {
                var types = Assembly.GetExecutingAssembly().GetTypes();
                foreach (var type in types)
                {
                    var methods = type.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                    foreach (var method in methods)
                    {
                        var attributes = method.GetCustomAttributes(typeof(RuntimeInitializeOnLoadMethodAttribute), false);
                        if (attributes.Length > 0)
                        {
                            method.Invoke(null, null);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                mls.LogError(e);
            }

            mls.LogInfo("[Shrimp] Loaded!");

            string directoryName = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            Bundle = AssetBundle.LoadFromFile(Path.Combine(directoryName, "shrimp"));
            if (Bundle == null)
            {
                mls.LogError("Failed to load Shrimp assets.");
            }
            else
            {
                shrimpSpawnWeight = base.Config.Bind<int>("Spawn", "ShrimpSpawnWeight", 5, new ConfigDescription("Sets the shrimp spawn weight for every moons.", null, Array.Empty<object>()));

                setKorean = (bool)base.Config.Bind<bool>("Translation", "Enable Korean", false, "Set language to Korean.").Value;


                shrimpPrefab = Bundle.LoadAsset<GameObject>("Shrimp.prefab");
                shrimpEnemy = Bundle.LoadAsset<EnemyType>("ShrimpEnemy.asset");

                shrimpItemManager = Bundle.LoadAsset<GameObject>("ShrimpItemManager.prefab");


                footstep1 = Bundle.LoadAsset<AudioClip>("Footstep1.ogg");
                footstep2 = Bundle.LoadAsset<AudioClip>("Footstep2.ogg");
                footstep3 = Bundle.LoadAsset<AudioClip>("Footstep3.ogg");
                footstep4 = Bundle.LoadAsset<AudioClip>("Footstep4.ogg");

                List<AudioClip> footstepList = new List<AudioClip>();
                footstepList.Add(footstep1);
                footstepList.Add(footstep2);
                footstepList.Add(footstep3);
                footstepList.Add(footstep4);
                footsteps = footstepList.ToArray();

                dogEatItem = Bundle.LoadAsset<AudioClip>("DogEatObject.ogg");
                dogEatPlayer = Bundle.LoadAsset<AudioClip>("EatPlayer.ogg");
                bigGrowl = Bundle.LoadAsset<AudioClip>("BigGrowl.ogg");
                enragedScream = Bundle.LoadAsset<AudioClip>("DogRage.ogg");
                dogSprint = Bundle.LoadAsset<AudioClip>("DogSprint.ogg");
                ripPlayerApart = Bundle.LoadAsset<AudioClip>("RipPlayerApart.ogg");
                cry1 = Bundle.LoadAsset<AudioClip>("Cry1.ogg");
                dogHowl = Bundle.LoadAsset<AudioClip>("DogHowl.ogg");
                stomachGrowl = Bundle.LoadAsset<AudioClip>("StomachGrowl.ogg");

                eatenExplode = Bundle.LoadAsset<AudioClip>("eatenExplode.ogg");
                dogSneeze = Bundle.LoadAsset<AudioClip>("Sneeze.ogg");
                dogSatisfied = Bundle.LoadAsset<AudioClip>("PlayBow.ogg");

                shrimpTerminalKeyword = Bundle.LoadAsset<TerminalKeyword>("shrimpTK.asset");

                if (!setKorean)
                {
                    shrimpTerminalNode = Bundle.LoadAsset<TerminalNode>("ShrimpFile.asset");
                    shrimpTerminalNode.creatureName = "Shrimp";
                    shrimpTerminalKeyword.word = "shrimp";
                }
                else
                {
                    shrimpTerminalNode = Bundle.LoadAsset<TerminalNode>("ShrimpKoreanFile.asset");
                    //shrimpTerminalNode.displayText = "쉬림프\r\n\r\n시구르드의 위험 수준: 60%\r\n\r\n\n학명: 카니스피리투스-아르테무스\r\n\r\n쉬림프는 개를 닮은 생명체로 Upturned Inn의 첫 번째 세입자로 알려져 있습니다. 평소에는 상대적으로 우호적이며, 호기심을 가지고 인간을 따라다닙니다. 불행하게도 그는 위험할 정도로 굉장한 식욕을 가지고 있습니다.\r\n생물학적 특성으로 인해, 그는 대부분의 다른 생물보다 훨씬 더 독특한 위장 기관을 가지고 있습니다. 위 내막은 유연하면서도 견고하기 때문에 어떤 물체라도 영양분을 소화하고 흡수할 수 있습니다.\r\n그러나 이러한 진화적 적응은 자연적으로 빠른 신진대사의 결과일 가능성이 높습니다. 그는 영양분을 너무 빨리 사용하기 때문에 생존하려면 하루에 여러 끼를 먹어야 합니다.\r\n칼로리 소비율이 다양하기 때문에 식사 사이의 시간이 일정하지 않습니다. 이는 몇 시간에서 몇 분까지 지속될 수 있으며, 쉬림프가 오랫동안 무언가를 먹지 않으면 매우 포악해지며 따라다니던 사람을 쫒습니다.\r\n\r\n버려진 건물에 사는 것으로 알려진 쉬림프는 버려진 공장이나 사무실에서 폐철물을 찾아다니는 것으로 발견할 수 있습니다. 그렇다고 다른 곳에서 그를 찾을 수 없다는 말은 아닙니다. 그는 일반적으로 고독한 사냥꾼이며, 때로는 전문적인 추적자가 되기도 합니다.\r\n\r\n시구르드의 노트: 이 녀석이 으르렁거리는 소리를 듣게 된다면, 먹이를 줄 수 있는 무언가를 가지고 있기를 바라세요. 아니면 당신이 이 녀석의 식사가 될 거예요.\r\n맹세컨대... 다시는 내 뒤에서 이 녀석을 보고 싶지 않아.\r\n\r\n\r\nIK: <i>손님, 슬퍼하지 마세요! 쉬림프는 당신을 싫어하지 않는답니다.\r\n걔는 그냥... 배고플 뿐이에요.</i>\r\n\r\n";
                    shrimpTerminalNode.creatureName = "쉬림프";
                    shrimpTerminalKeyword.word = "쉬림프";
                }

                ShrimpEnemyAI shrimpAI = shrimpPrefab.AddComponent<ShrimpEnemyAI>();
                shrimpAI.enemyType = shrimpEnemy;
                shrimpAI.creatureAnimator = shrimpPrefab.transform.GetChild(0).GetChild(1).GetComponent<Animator>();
                shrimpAI.creatureVoice = shrimpPrefab.transform.GetChild(0).GetChild(3).GetChild(0).GetComponent<AudioSource>();
                shrimpAI.growlAudio = shrimpPrefab.transform.GetChild(0).GetChild(3).GetChild(1).GetComponent<AudioSource>();
                shrimpAI.dogRageAudio = shrimpPrefab.transform.GetChild(0).GetChild(3).GetChild(2).GetComponent<AudioSource>();
                shrimpAI.hungerAudio = shrimpPrefab.transform.GetChild(0).GetChild(3).GetChild(3).GetComponent<AudioSource>();
                shrimpAI.sprintAudio = shrimpPrefab.transform.GetChild(0).GetChild(3).GetChild(4).GetComponent<AudioSource>();
                shrimpAI.mouthTransform = shrimpPrefab.transform.GetChild(0).GetChild(1).GetChild(0).GetChild(0).GetChild(0).GetChild(0).GetChild(2)
                    .GetChild(0).GetChild(0).GetChild(0).GetChild(4);
                shrimpAI.creatureSFX = shrimpPrefab.GetComponent<AudioSource>();
                shrimpAI.dieSFX = dogSneeze;

                shrimpAI.AIIntervalTime = 0.2f;
                shrimpAI.updatePositionThreshold = 1;
                shrimpAI.syncMovementSpeed = 0.22f;

                shrimpAI.chitterSFX = new AudioClip[1];
                shrimpAI.chitterSFX[0] = dogSatisfied;
                shrimpAI.angryScreechSFX = new AudioClip[1];
                shrimpAI.angryScreechSFX[0] = dogEatItem;

                shrimpAI.angryVoiceSFX = dogHowl;
                shrimpAI.bugFlySFX = footstep4;
                shrimpAI.hitPlayerSFX = dogEatItem;

                shrimpPrefab.transform.GetChild(0).GetComponent<EnemyAICollisionDetect>().mainScript = shrimpAI;

                shrimpItemManager.AddComponent<ShrimpItemManager>();

                shrimpEnemy.enemyPrefab = shrimpPrefab;

                LethalLib.Modules.NetworkPrefabs.RegisterNetworkPrefab(shrimpPrefab);
                LethalLib.Modules.NetworkPrefabs.RegisterNetworkPrefab(shrimpItemManager);

                Enemies.RegisterEnemy(shrimpEnemy, shrimpSpawnWeight.Value, Levels.LevelTypes.All, Enemies.SpawnType.Default, shrimpTerminalNode, shrimpTerminalKeyword);

                base.Logger.LogInfo("Successfully loaded assets!");

                Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), (string)null);
            }
        }
    }
}