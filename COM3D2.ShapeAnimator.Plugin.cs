//コンパイル
//C:\Windows\Microsoft.NET\Framework\v3.5\csc /t:library /lib:..\CM3D2x64_Data\Managed /r:UnityEngine.dll /r:UnityInjector.dll /r:Assembly-CSharp.dll CM3D2.ShapeAnimator.Plugin.cs
//
//シバリスのUnityInjectorフォルダ内でのコンパイル
//C:\Windows\Microsoft.NET\Framework\v3.5\csc /t:library /lib:lib /r:UnityEngine.dll /r:UnityInjector.dll /r:Assembly-CSharp.dll COM3D2.ShapeAnimator.Plugin.cs

using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityInjector;
using UnityInjector.Attributes;
using System.Xml;
using System.Text.RegularExpressions;
using UnityEngine.SceneManagement;

namespace COM3D2.ShapeAnimator
{
    [
	PluginFilter("COM3D2x64"),
    PluginFilter("COM3D2VRx64"),
    PluginFilter("COM3D2OHx64"),
    PluginFilter("COM3D2OHVRx64"),
    PluginName("ShapeAnimator"),
    PluginVersion("0.3.8.3")]
    public class ShapeAnimator : PluginBase
    {
        private readonly static string PLUGIN_NAME = "ShapeAnimator";
        private readonly static string PLUGIN_VERSION = "0.3.8.3";
        private readonly static int WINDOW_ID = 190;

        //4     エディット(Chu-B Lip)
        //5     エディット
        //10    夜伽(Chu-B Lip)
        //14    夜伽
        //8     男エディット(Chu-B Lip)
        //12    男エディット
        //11    イベント一般(Chu-B Lip)
        //15    イベント一般
        //24    回想モード
        //21    撮影モード(Chu-B Lip)
        //26    撮影モード
        //3     執務室(複数メイドプラグイン用)
        //18    メイドバトル
        //4, 20, 22, 26, 28 ダンス
        //3, 16, 18, 20, 22 ダンス(Chu-B Lip)
        private readonly static int[] EnableSceneLevel = new int[] { 5, 14, 12, 15, 24, 26, 3, 18, 43};
        private readonly static int[] EnableSceneLevelCBL = new int[] { 4, 10, 8, 11, 21, 16, 18, 20, 22 };
        private bool isChubLip = false;
        private bool isDance = false;
        private bool isDanceInit = false;

//        private CameraMain cameraMain;

        private XMLManager xml;
        private List<DataManager> dm;
        private MaidMgr mm;
        private ComboBox combo;
        private GroupMgr gm;
        private static NumericInputWindow numWindow;

        private bool bEnablePlugin = false;
        private bool bEditScene = false;
        private bool bEditSceneEnd = false;

        private bool bOnLoad = false;

        //key
        //private string keyShowPanel = "f4";
        private ShortCutKey hotkey;
        private float sliderRange = 1.0f;

        //GUI
        private Rect rectWin = new Rect();
        private Rect rectPopUp = new Rect();
        private Vector2 v2ScreenSize = Vector2.zero;
        private bool bGui = false;
        private bool bGuiPopUp = false;
        private float fWinHeight = 0f;

        private readonly static string[] sButtonText = new string[] { "アニメなし", "増加", "減少", "反復", "ランダム" };
        private int iConfirmRemove = -1;
        private int iActivePopUp = -1;
        private int iReturnPopUp = -1;
        private int iMessageLabelTimer = -1;
        private string sMessageLabel = string.Empty;
        //private bool bGuiOnMouse = true;

        private enum eFillter {ALL, NORM, ID, NAME, BANPEI};
        private int iFillterNo = (int)eFillter.ALL;
        private int imaidFillterAssign = 0;

        private static Regex regexNameAssign;

        private static string[] sIgnoreKeys;
        private static string[] sComboMenus;
        private static int iComboNum;

        private class MaidMgr
        {
            private CharacterMgr cm { get; set; }
            public List<Maid> listMaid { get; private set; }
            public List<string> listName { get; private set; }
            public bool bUpdate { get; set; }
            public bool bFindStock { get; set; }

            public MaidMgr()
            {
                listMaid = new List<Maid>();
                listName = new List<string>();
                cm = GameMain.Instance.CharacterMgr;
            }

            public static bool IsValid(Maid m)
            {
                if (m == null || m.body0 == null || !m.Visible)
                    return false;
                return true;
            }

            public bool Find()
            {
                List<Maid> _listMaid = new List<Maid>();

                for (int i = 0; i < cm.GetMaidCount(); i++)
                {
                    Maid maid = cm.GetMaid(i);
                    if (MaidMgr.IsValid(maid))
                        _listMaid.Add(maid);
                }

                if (bFindStock)
                {
                    List<Maid> _listStockMaid = cm.GetStockMaidList();

                    for (int i = 0; i < _listStockMaid.Count; i++)
                    {
                        if (MaidMgr.IsValid(_listStockMaid[i]) && !_listMaid.Contains(_listStockMaid[i]))
                            _listMaid.Add(_listStockMaid[i]);

                    }
                }

                listMaid.Clear();
                listName.Clear();

                listMaid = new List<Maid>(_listMaid);

                for (int i = 0; i < listMaid.Count; i++)
                {
                    listName.Add(listMaid[i].status.lastName + " " + listMaid[i].status.firstName);
                }

                bUpdate = false;
                return listMaid.Count == 0 ? false : true;
            }

            public void Clear()
            {
                listMaid.Clear();
                listName.Clear();
                bUpdate = true;
                bFindStock = false;
            }
        }

        private class GroupMgr
        {
            private List<DataManager> dm { get; set; }
            private Dictionary<int, int> dictNumberAndMaster { get; set; }
            public string[] sMenus { get; set; }

            private static readonly Dictionary<enumMenuKey, string> dictMenuKey;
            private enum enumMenuKey
            {
                none_,
                master_,
                new_,
                del_
            }

            static GroupMgr()
            {
                dictMenuKey = new Dictionary<enumMenuKey, string>()
                {
                    { enumMenuKey.none_, "グループなし" },
                    { enumMenuKey.master_, "マスター" },
                    { enumMenuKey.new_, "新規グループ" },
                    { enumMenuKey.del_, "グループ削除" },
                };
            }

            public GroupMgr(List<DataManager> dm)
            {
                this.dm = dm;
                dictNumberAndMaster = new Dictionary<int, int>();
                sMenus = new string[0];
            }

            public void MakeDict()
            {
                dictNumberAndMaster = new Dictionary<int, int>();

                HashSet<int> hashGroupNum = new HashSet<int>();
                for (int i = 0; i < dm.Count; i++)
                {
                    if (dm[i].group >= 0)
                        hashGroupNum.Add(dm[i].group);
                }

                List<int> listGroupNum = hashGroupNum.ToList();
                listGroupNum.Sort();

                for (int i = 0; i < listGroupNum.Count; i++)
                {
                    dictNumberAndMaster.Add(listGroupNum[i], -1);
                }

                for (int i = 0; i < dm.Count; i++)
                {
                    if (dm[i].groupMaster)
                    {
                        if (dictNumberAndMaster.ContainsKey(dm[i].group))
                            dictNumberAndMaster[dm[i].group] = i;
                    }
                }
            }

            private bool IsValidMaster(int iGroup)
            {
                if (dictNumberAndMaster.ContainsKey(iGroup))
                {
                    return dictNumberAndMaster[iGroup] >= 0 && dm[dictNumberAndMaster[iGroup]].enable;
                }
                return false;
            }

            public bool IsSkipAnimate(int iDm)
            {
                return dm[iDm].group >= 0 && !dm[iDm].groupMaster && IsValidMaster(dm[iDm].group);
            }

            public int GetMaster(int iGroup)
            {
                return dictNumberAndMaster[iGroup];
            }

            public string[] GetMenu(int iDm)
            {
                ChkSolitude();
                if (dm[iDm].group < 0)
                {
                    sMenus = new string[dictNumberAndMaster.Count + 1];
                    SetMenuNumbers(0);
                    sMenus[dictNumberAndMaster.Count] = dictMenuKey[enumMenuKey.new_];
                }
                else if (dm[iDm].groupMaster)
                {
                    sMenus = new string[dictNumberAndMaster.Count + 3];
                    sMenus[0] = dictMenuKey[enumMenuKey.none_];
                    SetMenuNumbers(1);
                    sMenus[dictNumberAndMaster.Count + 1] = dictMenuKey[enumMenuKey.new_];
                    sMenus[dictNumberAndMaster.Count + 2] = dictMenuKey[enumMenuKey.del_];
                }
                else
                {
                    sMenus = new string[dictNumberAndMaster.Count + 4];
                    sMenus[0] = dictMenuKey[enumMenuKey.none_];
                    SetMenuNumbers(1);
                    sMenus[dictNumberAndMaster.Count + 1] = dictMenuKey[enumMenuKey.master_];
                    sMenus[dictNumberAndMaster.Count + 2] = dictMenuKey[enumMenuKey.new_];
                    sMenus[dictNumberAndMaster.Count + 3] = dictMenuKey[enumMenuKey.del_];
                }

                return sMenus;
            }

            private void SetMenuNumbers(int iStart)
            {
                foreach (KeyValuePair<int, int> kvp in dictNumberAndMaster)
                {
                    sMenus[iStart++] = kvp.Key.ToString();
                }
            }

            private void Create(int iDm)
            {
                int i = 0;
                while (dictNumberAndMaster.ContainsKey(i))
                    i++;
                dictNumberAndMaster.Add(i, iDm);
                dm[iDm].group = i;
                dm[iDm].groupMaster = true;
            }

            private void Delete(int iDm)
            {
                int iDel = dm[iDm].group;
                if (dictNumberAndMaster.ContainsKey(iDel))
                {
                    dictNumberAndMaster.Remove(iDel);
                    for (int i = 0; i < dm.Count; i++)
                    {
                        if (dm[i].group == iDel)
                        {
                            dm[i].group = -1;
                            dm[i].groupMaster = false;
                        }
                    }
                }
            }

            private void Release(int iDm)
            {
                int iGroup = dm[iDm].group;
                if (!dictNumberAndMaster.ContainsKey(iGroup))
                    return;

                dm[iDm].group = -1;
                dm[iDm].groupMaster = false;

                if (dictNumberAndMaster[iGroup] == iDm)
                    dictNumberAndMaster[iGroup] = -1;

                for (int i = 0; i < dm.Count; i++)
                {
                    if (dm[i].group == iGroup)
                        return;
                }
                dictNumberAndMaster.Remove(iGroup);
            }

            private void SetMaster(int iDm)
            {
                int iGroup = dm[iDm].group;
                if (!dictNumberAndMaster.ContainsKey(iGroup))
                    return;

                dictNumberAndMaster[iGroup] = iDm;
                for (int i = 0; i < dm.Count; i++)
                {
                    if (dm[i].group == iGroup)
                    {
                        if (i == iDm)
                            dm[i].groupMaster = true;
                        else
                            dm[i].groupMaster = false;
                    }
                }
            }

            private void ChkSolitude()
            {
                Dictionary<int, bool> dictChk = new Dictionary<int, bool>();
                foreach (KeyValuePair<int, int> kvp in dictNumberAndMaster)
                {
                    dictChk.Add(kvp.Key, false);
                }

                for (int i = 0; i < dm.Count; i++)
                {
                    if (dictChk.ContainsKey(dm[i].group))
                        dictChk[dm[i].group] = true;
                }

                foreach (KeyValuePair<int, bool> kvp in dictChk)
                {
                    if (!kvp.Value)
                        dictNumberAndMaster.Remove(kvp.Key);
                }
            }

            public void ClickMenu(int iDm, int x)
            {
                int iNumber = 0;
                if (int.TryParse(sMenus[x], out iNumber))
                {
                    if (dm[iDm].group != iNumber)
                        dm[iDm].groupMaster = false;
                    dm[iDm].group = iNumber;
                }
                else
                {
                    if (sMenus[x] == dictMenuKey[enumMenuKey.none_])
                    {
                        Release(iDm);
                    }
                    else if (sMenus[x] == dictMenuKey[enumMenuKey.master_])
                    {
                        SetMaster(iDm);
                    }
                    else if (sMenus[x] == dictMenuKey[enumMenuKey.new_])
                    {
                        Create(iDm);
                    }
                    else if (sMenus[x] == dictMenuKey[enumMenuKey.del_])
                    {
                        Delete(iDm);
                    }
                }

                MakeDict();
            }

            private HashSet<int> GetGroupNumbersHash()
            {
                HashSet<int> hashGroupNum = new HashSet<int>();
                for (int i = 0; i < dm.Count; i++)
                {
                    if (dm[i].group >= 0)
                        hashGroupNum.Add(dm[i].group);
                }
                return hashGroupNum;
            }
        }

        public void Awake()
        {
            UnityEngine.GameObject.DontDestroyOnLoad(this);
            isChubLip = Application.dataPath.Contains("COM3D2OH");

            string sIgnoreTxtPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) + @"\Config\ShapeAnimator_IgnoreKeys.txt";
            if (File.Exists(sIgnoreTxtPath))
            {
                sIgnoreKeys = File.ReadAllLines(sIgnoreTxtPath);
            }
            else
            {
                sIgnoreKeys = new string[0];
            }
        }

        public void Start()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.sceneUnloaded += OnSceneUnLoaded;

            //cameraMain = GameMain.Instance.MainCamera;
            xml = new XMLManager();
            dm = new List<DataManager>();
            mm = new MaidMgr();
            combo = new ComboBox(WINDOW_ID + 1);
            gm = new GroupMgr(dm);
            numWindow = new NumericInputWindow(WINDOW_ID + 1);

            regexNameAssign = new Regex(
                @"^\*.+?\*"
                );

            hotkey = new ShortCutKey();
        }

        public void OnSceneLoaded(Scene scene, LoadSceneMode sceneMode)
        {
            int level = scene.buildIndex;
            bEnablePlugin = false;
            bEditScene = false;
            bEditSceneEnd = false;
            bGui = false;
            iConfirmRemove = -1;
            iActivePopUp = -1;
            iReturnPopUp = -1;
            iMessageLabelTimer = -1;
            isDance = FindObjectOfType(typeof(DanceMain)) != null ? true : false ;
            isDanceInit = false;

            if (!isDance){
                // カラオケモードのキャラクター選択画面もダンス扱いしてみる
                // もしかしたら他のキャラクター選択画面も同じかもしれんがまあ悪さはしないやら
                if (scene.name == "SceneCharacterSelect"){
                    isDance = true;
                }
            }

//            UnityEngine.Debug.LogError("scene.name = " + scene.name + ", sceneMode = " + sceneMode;
//            UnityEngine.Debug.LogError(" isDance : " + isDance);
//            UnityEngine.Debug.LogError(" DanceMain.KaraokeMode : " + DanceMain.KaraokeMode);

            mm.Clear();

            //有効にするシーンレベルを追加する場合は
            //「EnableSceneLevel」配列に数値を追加する
            if ((!isChubLip && EnableSceneLevel.Contains(level)) || (isChubLip && EnableSceneLevelCBL.Contains(level))
                || isDance )
            //    if (EnableSceneLevel.Contains(level))
            {
                LoadXML();
                bEnablePlugin = true;
                if ((!isChubLip && level == 5) || (isChubLip && level == 4))
                    bEditScene = true;
                if (level == 3 || bEditScene)
                    mm.bFindStock = true;
            }
        }

        public void OnSceneUnLoaded(Scene scene)
        {
        }

        public void OnGUI()
        {
            if (bGui)
            {
                GUIStyle gsWin = new GUIStyle("box");
                gsWin.fontSize = Utl.GetPix(12);
                gsWin.alignment = TextAnchor.UpperRight;

                float fWinWidth = gsWin.fontSize * 24;

                if (rectWin.width < 1)
                {
                    rectWin.Set(Screen.width - fWinWidth, 0, fWinWidth, fWinHeight);
                }

                if (fWinHeight != rectWin.height)
                {
                    rectWin.Set(rectWin.x, rectWin.y, fWinWidth, fWinHeight);
                }

                if (v2ScreenSize != new Vector2(Screen.width, Screen.height))
                {
                    rectWin.Set(rectWin.x, rectWin.y, fWinWidth, fWinHeight);
                    v2ScreenSize = new Vector2(Screen.width, Screen.height);
                }
                if (rectWin.x < 0 - rectWin.width * 0.9f)
                {
                    rectWin.x = 0;
                }
                else if (rectWin.x > v2ScreenSize.x - rectWin.width * 0.1f)
                {
                    rectWin.x = v2ScreenSize.x - rectWin.width;
                }
                else if (rectWin.y < 0 - rectWin.height * 0.9f)
                {
                    rectWin.y = 0;
                }
                else if (rectWin.y > v2ScreenSize.y - rectWin.height * 0.1f)
                {
                    rectWin.y = v2ScreenSize.y - rectWin.height;
                }
                rectWin = GUI.Window(WINDOW_ID, rectWin, GuiFunc, PLUGIN_NAME + PLUGIN_VERSION, gsWin);

                if (bGuiPopUp)
                {
                    GUI.Window(WINDOW_ID + 1, rectPopUp, GuiFuncPopUp, string.Empty, gsWin);
                    if (GetAnyMouseButtonDown() && !rectPopUp.Contains(new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y)))
                        bGuiPopUp = false;
                }

                

                //if (rectWin.Contains(new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y)))
                //{
                //    GameMain.Instance.MainCamera.SetControl(false);
                //    bGuiOnMouse = true;
                //}
                //else
                //{
                //    if (bGuiOnMouse)
                //        GameMain.Instance.MainCamera.SetControl(true);
                //    bGuiOnMouse = false;
                //}

                if (combo.show)
                {
                    combo.rect = GUI.Window(combo.WINDOW_ID, combo.rect, combo.GuiFunc, string.Empty, gsWin);
                    if (Utl.IsMouseOnRect(combo.rect))
                    {
                        if (Utl.GetAnyMouseDown() || Utl.GetMouseWheelUse())
                            Input.ResetInputAxes();
                    }
                }

                if (numWindow.show)
                {
                    numWindow.rect = GUI.Window(numWindow.WINDOW_ID, numWindow.rect, numWindow.GuiFunc, string.Empty, gsWin);
                }

                if (Utl.IsMouseOnRect(rectWin))
                {
                    if (Utl.GetAnyMouseDown() || Utl.GetMouseWheelUse())
                    {
                        Input.ResetInputAxes();
                        if (!Utl.IsMouseOnRect(combo.rect))
                            combo.show = false;
                    }
                }
            }
            //else
            //{
            //    if (bGuiOnMouse)
            //        GameMain.Instance.MainCamera.SetControl(true);
            //    bGuiOnMouse = false;
            //}
        }

        private bool GetAnyMouseButtonDown()
        {
            return Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2);
        }

        private bool IsMouseOnGUI()
        {
            Vector2 v2Mouse = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
            if (rectWin.Contains(v2Mouse))
                return true;
            if (rectPopUp.Contains(v2Mouse))
                return true;
            if (combo.rect.Contains(v2Mouse))
                return true;

            return false;
        }

        private void ChkMouseClick()
        {
            if (Input.GetMouseButtonUp(0) && IsMouseOnGUI())
            {
                Input.ResetInputAxes();
            }
        }

        Vector2 v2ScrollPos = Vector2.zero;
        float fScrollInnerHeight = 0f;

        private void GuiFunc(int winId)
        {
            #region Variable
            GUIStyle gsLabel = new GUIStyle("label");
            gsLabel.fontSize = Utl.GetPix(12);
            gsLabel.alignment = TextAnchor.UpperLeft;

            GUIStyle gsLabelR = new GUIStyle("label");
            gsLabelR.fontSize = Utl.GetPix(12);
            gsLabelR.alignment = TextAnchor.UpperRight;

            GUIStyle gsToggle = new GUIStyle("toggle");
            gsToggle.fontSize = Utl.GetPix(12);
            gsToggle.alignment = TextAnchor.MiddleLeft;

            GUIStyle gsButton = new GUIStyle("button");
            gsButton.fontSize = Utl.GetPix(12);
            gsButton.alignment = TextAnchor.MiddleCenter;

            GUIStyle gsText = new GUIStyle("textfield");
            gsText.fontSize = Utl.GetPix(12);
            gsText.alignment = TextAnchor.UpperLeft;

            float fFontSize = gsLabel.fontSize;
            float fItemHeight = gsLabel.fontSize * 1.5f;
            float fMargin = gsLabel.fontSize * 0.5f;

            bool bPopUp = false;
            int iDisable = -1;
            //int iChangemaid = -1;

            Rect rectInner = new Rect(fFontSize / 2f, gsLabel.fontSize + fMargin, rectWin.width - fFontSize, rectWin.height - fFontSize);
            Rect rectItem = new Rect(rectInner.x, rectInner.y, rectInner.width, fItemHeight);
            #endregion
            if (GUI.Button(new Rect(0f, 0f, Utl.GetPix(8) * 2, Utl.GetPix(8) * 2), "×", gsButton))
            {
                bGui = false;
            }

            if (mm.listMaid.Count == 0)
            {
                fWinHeight = rectItem.y + rectItem.height + fMargin;
                GUI.DragWindow();
                return;
            }

            rectItem.Set(rectInner.x, rectItem.y, rectInner.width / 2f, fItemHeight);
            GUI.Label(rectItem, "名前", gsLabel);
            rectItem.Set(rectItem.x + rectItem.width, rectItem.y, rectItem.width, fItemHeight);
            GUI.Label(rectItem, "キー名", gsLabel);

            Rect rectScrollView = new Rect(0f, rectItem.y + rectItem.height, rectWin.width, Mathf.Min(fScrollInnerHeight, Screen.height * 0.7f));
            Rect rectScroll = new Rect(0f, 0f, rectWin.width - fFontSize * 1.5f, fScrollInnerHeight);
            Rect rectScrollInner = new Rect(fFontSize * 0.5f, 0f, rectScroll.width - fFontSize, fScrollInnerHeight);

            rectItem.Set(0f, 0f, 0f, 0f);
            //v2ScrollPos = GUI.BeginScrollView(rectScroll, v2ScrollPos, rectScrollInner, false, true);
            v2ScrollPos = GUI.BeginScrollView(rectScrollView, v2ScrollPos, rectScroll);
            for (int i = 0; i < dm.Count; i++)
            {
                if (iFillterNo != (int)eFillter.ALL) {
                    if (dm[i].maidGuid != string.Empty) {
                        if (iFillterNo != (int)eFillter.ID) continue;
                    }
                    else if (dm[i].maidFixedAssign >= 0) {
                        if (iFillterNo != (int)eFillter.NAME){
                            continue;
                        }
                        else {
                            if (dm[i].maidFixedAssign != imaidFillterAssign) continue;
                        }
                    }
                    else {
                        if (iFillterNo != (int)eFillter.NORM) continue;
                    }
                }

                GUI.enabled = dm[i].enable;

                string sTmp;

                Color clr = GUI.color;
                if (dm[i].groupMaster)
                    GUI.color = Color.green;
                rectItem.Set(rectScrollInner.x, rectItem.y + fMargin + fItemHeight / 10f, fFontSize * 2, fItemHeight * 1.1f);
                if (GUI.Button(rectItem, dm[i].group < 0 ? "G" : dm[i].group.ToString(), gsButton))
                {
                    iComboNum = i;
                    sComboMenus = gm.GetMenu(i);
                    combo.Set(
                        new Rect(rectWin.x + rectItem.x, rectWin.y + rectItem.y + fItemHeight + rectScrollView.y - v2ScrollPos.y, rectScrollInner.width * 0.6f, fItemHeight * sComboMenus.Length),
                        sComboMenus,
                        (int)fFontSize,
                        (x) =>
                        {
                            gm.ClickMenu(iComboNum, x);
                        });
                }
                GUI.color = clr;

                rectItem.Set(rectItem.x + rectItem.width, rectItem.y, rectScrollInner.width / 2f - fFontSize * 2, rectItem.height);
                sTmp = GUI.TextField(rectItem, dm[i].name, gsText);
                if (sTmp != dm[i].name)
                {
                    dm[i].name = sTmp;
                    OnNameFieldChange(i);
                }

                rectItem.Set(rectItem.x + rectItem.width, rectItem.y, rectItem.width, rectItem.height);
                dm[i].tag = GUI.TextField(rectItem, dm[i].tag, gsText);

                rectItem.x += rectItem.width;
                rectItem.width = fFontSize * 2;
                if (GUI.Button(rectItem, "+", gsButton))
                {
                    if (dm[i].maid >= 0)
                    {
                        sComboMenus = GetAllKeys(mm.listMaid[dm[i].maid].body0);
                        iComboNum = i;
                        float fItemYPos = rectWin.y + rectItem.y + fItemHeight + rectScrollView.y - v2ScrollPos.y;
                        fItemYPos = fItemYPos > (Screen.height / 2f) ? fItemYPos - fItemHeight - Mathf.Min(fItemHeight * sComboMenus.Length, Screen.height * 0.5f) : fItemYPos;
                        combo.Set(
                            new Rect(rectWin.x + rectScrollInner.x + (rectScrollInner.width / 2f), fItemYPos, rectScrollInner.width / 2f + fFontSize, fItemHeight * sComboMenus.Length),
                            sComboMenus,
                            (int)fFontSize,
                            (x) =>
                            {
                                dm[iComboNum].tag = sComboMenus[x];
                            });
                    }
                }

                if (dm[i].enable && !dm[i].fold)
                {
                    rectItem.Set(rectScrollInner.x, rectItem.y + rectItem.height + fMargin, rectScrollInner.width, fItemHeight);
                    dm[i].val = GUI.HorizontalSlider(rectItem, dm[i].val, 0f, sliderRange);

                    rectItem.Set(rectScrollInner.x, rectItem.y + rectItem.height + fMargin, fFontSize * 2, fItemHeight);
                    if (GUI.Button(rectItem, "<", gsButton))
                    {
                        if (--dm[i].maidSel < 0)
                            dm[i].maidSel = mm.listMaid.Count - 1;
                        dm[i].maid = dm[i].maidSel;
                    }
                    rectItem.x += rectItem.width;
                    if (GUI.Button(rectItem, ">", gsButton))
                    {
                        if (++dm[i].maidSel >= mm.listMaid.Count)
                            dm[i].maidSel = 0;
                        dm[i].maid = dm[i].maidSel;
                    }

                    rectItem.x += rectItem.width;
                    // 모든 캐릭 적용 여부
                    if (GUI.Button(rectItem, "A", gsButton))
                    {
                        switch (dm[i].mod)
                        {
                            case DataManager.ModType.none:
                                dm[i].mod = DataManager.ModType.All;
                                Debug.Log("모든캐릭 적용:" + dm[i].tag);
                                break;
                            case DataManager.ModType.All:
                                //break;
                            default:
                                dm[i].mod = DataManager.ModType.none;
                                Debug.Log("일반 적용:" + dm[i].tag);
                                break;
                        }


                    }

                    rectItem.Set(rectItem.x + rectItem.width, rectItem.y, rectInner.width - fFontSize * 10, fItemHeight);
                    GUI.enabled = dm[i].maidFixedAssign < 0;
                    GUI.Label(rectItem, dm[i].maidGuid == string.Empty ? mm.listName[dm[i].maidSel] : "!!" + dm[i].maidNameByGuid, gsLabel);
                    GUI.enabled = true;

                    rectItem.Set(rectScrollInner.width - fFontSize * 5.5f, rectItem.y, fFontSize * 6, fItemHeight);
                    string sMaidAssinButtonName;
                    if (dm[i].maidGuid != string.Empty)
                        sMaidAssinButtonName = "ID指定";
                    else if (dm[i].maidFixedAssign >= 0)
                        sMaidAssinButtonName = "이름지정";
                    else
                        sMaidAssinButtonName = "일반지정";

                    if (GUI.Button(rectItem, sMaidAssinButtonName, gsButton))
                    {
                        OnClickMaidAssignButton(i);
                    }

                    if(gm.IsSkipAnimate(i))
                    {
                        rectItem.Set(rectScrollInner.x, rectItem.y + rectItem.height + fMargin, fFontSize * 4, fItemHeight);
                        dm[i].groupReverse = GUI.Toggle(rectItem, dm[i].groupReverse, "반전", gsToggle);

                        rectItem.Set(rectScrollInner.width - fFontSize * 3.5f, rectItem.y, fFontSize * 4, fItemHeight);
                        if (GUI.Button(rectItem, "Num", gsButton))
                        {
                            numWindow.Set(new Vector2(rectWin.x + rectWin.width - fFontSize * 2, rectWin.y + rectItem.y + fItemHeight + rectScrollView.y - v2ScrollPos.y), fFontSize * 12, rectWin.width - fFontSize * 3, (int)fFontSize,
                                new float[3] { dm[i].groupOffset, dm[i].groupModulate2, dm[i].groupPoint },
                                i, true,
                                (x) =>
                                {
                                    dm[numWindow.iDmNumber].groupOffset = x[0];
                                    dm[numWindow.iDmNumber].groupModulate2 = x[1];
                                    dm[numWindow.iDmNumber].groupPoint = x[2];
                                });
                        }

                        //rectItem.x = rectScrollInner.x;
                        //rectItem.width = fFontSize * 6;
                        //rectItem.y += rectItem.height + fMargin;
                        //GUI.Label(rectItem, "幅", gsLabel);

                        //rectItem.x = rectScrollInner.width - fFontSize * 5.5f;
                        //rectItem.width = fFontSize * 6;
                        //sTmp = Utl.DrawTextFieldF(rectItem, dm[i].groupModulate2Text, 0f, 1f, gsText);
                        //if (sTmp != dm[i].groupModulate2Text)
                        //{
                        //    dm[i].groupModulate2Text = sTmp;
                        //    dm[i].groupModulate2 = float.Parse(sTmp);
                        //}

                        rectItem.x = rectScrollInner.x;
                        rectItem.width = rectScrollInner.width;
                        rectItem.y += rectItem.height;
                        dm[i].groupOffset = GUI.HorizontalSlider(rectItem, dm[i].groupOffset, -1f, 1f);

                        rectItem.y += rectItem.height;
                        dm[i].groupModulate2 = GUI.HorizontalSlider(rectItem, dm[i].groupModulate2, 0f, 1f);
                        dm[i].groupModulate2 = GUI.HorizontalSlider(rectItem, dm[i].groupModulate2, 0f, 1f);


                        //rectItem.x = rectScrollInner.x;
                        //rectItem.width = fFontSize * 6;
                        //rectItem.y += rectItem.height;
                        //GUI.Label(rectItem, "基準点", gsLabel);

                        //rectItem.x = rectScrollInner.width - fFontSize * 5.5f;
                        //rectItem.width = fFontSize * 6;
                        //sTmp = Utl.DrawTextFieldF(rectItem, dm[i].groupPointText, 0f, 1f, gsText);
                        //if(sTmp != dm[i].groupPointText)
                        //{
                        //    dm[i].groupPointText = sTmp;
                        //    dm[i].groupPoint = float.Parse(sTmp);
                        //}

                        //rectItem.x = rectScrollInner.x;
                        //rectItem.width = rectScrollInner.width;
                        rectItem.y += rectItem.height;
                        dm[i].groupPoint = GUI.HorizontalSlider(rectItem, dm[i].groupPoint, 0f, 1f);
                    }
                    else
                    {
                        rectItem.Set(rectScrollInner.x, rectItem.y + rectItem.height + fMargin, fFontSize * 6, fItemHeight);
                        if (GUI.Button(rectItem, sButtonText[(int)dm[i].animeType], gsButton))
                        {
                            iActivePopUp = i;
                            iReturnPopUp = (int)dm[i].animeType;
                            bGuiPopUp = !bGuiPopUp;
                            if (bGuiPopUp)
                                bPopUp = true;
                        }
                        if (bGuiPopUp && bPopUp)
                        {
                            rectPopUp.Set(rectWin.x + rectItem.x, rectWin.y + rectScrollView.y + rectItem.y + rectItem.height - v2ScrollPos.y, rectItem.width, rectItem.height * 5);
                            bPopUp = false;
                        }

                        if (dm[i].animeType != DataManager.AnimeType.none)
                        {
                            rectItem.Set(rectItem.x + rectItem.width + fFontSize, rectItem.y, fFontSize * 7, fItemHeight);
                            if (GUI.Button(rectItem, "タイマー設定", gsButton))
                            {
                                dm[i].extraSetting = !dm[i].extraSetting;
                            }

                            if (dm[i].actionTimeMax != 0 && dm[i].actionIntervalMax != 0)
                            {
                                rectItem.Set(rectItem.x + rectItem.width, rectItem.y, fFontSize * 3, fItemHeight);
                                GUI.Label(rectItem, dm[i].actionTimer >= 0 ? dm[i].actionTimer.ToString() : (-dm[i].actionTime - dm[i].actionTimer).ToString(), gsLabelR);
                            }
                        }

                        if (dm[i].animeType == DataManager.AnimeType.none)
                        {
                            rectItem.Set(rectScrollInner.width - fFontSize * 1.5f, rectItem.y, fFontSize * 2, fItemHeight);
                            if (GUI.Button(rectItem, "|", gsButton))
                            {
                                dm[i].val = 0.5f;
                            }
                        }
                        else
                        {
                            rectItem.Set(rectScrollInner.width - fFontSize * 3.5f, rectItem.y, fFontSize * 4, fItemHeight);
                            if (GUI.Button(rectItem, "Num", gsButton))
                            {
                                numWindow.Set(new Vector2(rectWin.x + rectWin.width - fFontSize * 2, rectWin.y + rectItem.y + fItemHeight + rectScrollView.y - v2ScrollPos.y), fFontSize * 12, rectWin.width - fFontSize * 3, (int)fFontSize,
                                    new float[3] { dm[i].modulate1, dm[i].modulate2, dm[i].point },
                                    i, false,
                                    (x) =>
                                    {
                                        dm[numWindow.iDmNumber].modulate1 = x[0];
                                        dm[numWindow.iDmNumber].modulate2 = x[1];
                                        dm[numWindow.iDmNumber].point = x[2];
                                    });
                            }
                        }

                        //if (dm[i].animeType != DataManager.AnimeType.none)
                        //    GUI.enabled = false;

                        //rectItem.Set(rectScrollInner.width - fFontSize * 1.5f, rectItem.y, fFontSize * 2, fItemHeight);

                        //GUI.enabled = dm[i].enable;

                        if (dm[i].animeType != DataManager.AnimeType.none)
                        {
                            rectItem.Set(rectScrollInner.x, rectItem.y + fItemHeight, rectScrollInner.width, fItemHeight);
                            dm[i].modulate1 = GUI.HorizontalSlider(rectItem, dm[i].modulate1, 0f, 1f);

                            rectItem.Set(rectScrollInner.x, rectItem.y + fItemHeight, rectScrollInner.width, fItemHeight);
                            dm[i].modulate2 = GUI.HorizontalSlider(rectItem, dm[i].modulate2, 0f, 1f);

                            rectItem.Set(rectScrollInner.x, rectItem.y + fItemHeight, rectScrollInner.width, fItemHeight);
                            dm[i].point = GUI.HorizontalSlider(rectItem, dm[i].point, 0f, 1f);

                            if (dm[i].extraSetting)
                            {
                                rectItem.Set(rectScrollInner.x, rectItem.y + fItemHeight, rectScrollInner.width / 2f, fItemHeight);
                                GUI.Label(rectItem, "実行時間: " + dm[i].actionTimeMin.ToString() + "/" + dm[i].actionTimeMax.ToString() + "/" + dm[i].actionTime.ToString(), gsLabel);

                                rectItem.Set(rectItem.x + rectItem.width, rectItem.y, rectItem.width, fItemHeight);
                                GUI.Label(rectItem, "実行間隔: " + dm[i].actionIntervalMin.ToString() + "/" + dm[i].actionIntervalMax.ToString() + "/" + dm[i].actionInterval.ToString(), gsLabel);

                                rectItem.Set(rectScrollInner.x, rectItem.y + fItemHeight, rectScrollInner.width / 2f, fItemHeight);
                                dm[i].actionTimeMin = (int)GUI.HorizontalSlider(rectItem, dm[i].actionTimeMin, 0f, 600f);
                                if (dm[i].actionTimeMin > dm[i].actionTimeMax)
                                    dm[i].actionTimeMax = dm[i].actionTimeMin;

                                rectItem.Set(rectItem.x + rectItem.width, rectItem.y, rectItem.width, fItemHeight);
                                dm[i].actionIntervalMin = (int)GUI.HorizontalSlider(rectItem, dm[i].actionIntervalMin, 0f, 600f);
                                if (dm[i].actionIntervalMin > dm[i].actionIntervalMax)
                                    dm[i].actionIntervalMax = dm[i].actionIntervalMin;

                                rectItem.Set(rectScrollInner.x, rectItem.y + fItemHeight, rectScrollInner.width / 2f, fItemHeight);
                                dm[i].actionTimeMax = (int)GUI.HorizontalSlider(rectItem, dm[i].actionTimeMax, 0f, 600f);
                                if (dm[i].actionTimeMin > dm[i].actionTimeMax)
                                    dm[i].actionTimeMin = dm[i].actionTimeMax;

                                rectItem.Set(rectItem.x + rectItem.width, rectItem.y, rectItem.width, fItemHeight);
                                dm[i].actionIntervalMax = (int)GUI.HorizontalSlider(rectItem, dm[i].actionIntervalMax, 0f, 600f);
                                if (dm[i].actionIntervalMin > dm[i].actionIntervalMax)
                                    dm[i].actionIntervalMin = dm[i].actionIntervalMax;

                                rectItem.Set(rectScrollInner.x, rectItem.y + fItemHeight, fFontSize * 7, fItemHeight);
                                GUI.Label(rectItem, "대기중의값은");

                                bool bZero = false;
                                bool bPoint = false;
                                bool bTmp;
                                switch (dm[i].timerWaitType)
                                {
                                    case DataManager.TimerWaitType.zero:
                                        bZero = true;
                                        break;
                                    case DataManager.TimerWaitType.point:
                                        bPoint = true;
                                        break;
                                }

                                rectItem.Set(rectItem.x + rectItem.width, rectItem.y, fFontSize * 5, fItemHeight);
                                bTmp = GUI.Toggle(rectItem, bZero, "왼쪽에", gsToggle);
                                if (bZero != bTmp)
                                {
                                    bZero = bTmp;
                                    if (bZero)
                                        dm[i].timerWaitType = DataManager.TimerWaitType.zero;
                                    else
                                        dm[i].timerWaitType = DataManager.TimerWaitType.none;
                                }
                                rectItem.Set(rectItem.x + rectItem.width, rectItem.y, fFontSize * 5, fItemHeight);
                                bTmp = GUI.Toggle(rectItem, bPoint, "基準点へ", gsToggle);
                                if (bPoint != bTmp)
                                {
                                    bPoint = bTmp;
                                    if (bPoint)
                                        dm[i].timerWaitType = DataManager.TimerWaitType.point;
                                    else
                                        dm[i].timerWaitType = DataManager.TimerWaitType.none;
                                }
                            }
                        }
                        else
                        {
                            dm[i].extraSetting = false;
                        }
                    }
                }
                else
                {
                    rectItem.Set(rectScrollInner.x + fFontSize * 4, rectItem.y + fItemHeight + fMargin, rectInner.width - fFontSize * 16, fItemHeight);
                    GUI.Label(rectItem, dm[i].maidGuid == string.Empty ? mm.listName[dm[i].maidSel] : "!!" + dm[i].maidNameByGuid, gsLabel);
                    rectItem.y -= fItemHeight + fMargin;
                }

                GUI.enabled = true;
                rectItem.Set(rectScrollInner.x, rectItem.y + fItemHeight + fMargin, fFontSize * 2, fItemHeight);
                if (GUI.Button(rectItem, "△", gsButton))
                {
                    MoveDM(i, true);
                }
                rectItem.x += rectItem.width;
                if (GUI.Button(rectItem, "▽", gsButton))
                {
                    MoveDM(i, false);
                }

                GUI.enabled = dm[i].enable;
                rectItem.Set(rectScrollInner.width - fFontSize * 11.5f, rectItem.y, fFontSize * 4, fItemHeight);
                if (GUI.Button(rectItem, dm[i].fold ? "↓" : "↑", gsButton))
                {
                    dm[i].fold = !dm[i].fold;
                }

                GUI.enabled = true;
                rectItem.Set(rectScrollInner.width - fFontSize * 7.5f, rectItem.y, fFontSize * 4, fItemHeight);
                if (GUI.Button(rectItem, dm[i].enable ? "해제" : "유효", gsButton))
                {
                    dm[i].enableClick = true;
                    dm[i].enable = !dm[i].enable;
                    if (!dm[i].enable)
                    {
                        //dm[i].val = 0f;
                        iDisable = i;
                    }
                }

                rectItem.Set(rectScrollInner.width - fFontSize * 3.5f, rectItem.y, fFontSize * 4, fItemHeight);
                if (GUI.Button(rectItem, "삭제", gsButton))
                {
                    iConfirmRemove = i;
                }
                GUI.enabled = dm[i].enable;

                rectItem.Set(rectScrollInner.x, rectItem.y + rectItem.height + fItemHeight / 10f, rectScrollInner.width, 1);
                GUI.DrawTexture(rectItem, Texture2D.whiteTexture);

                //rectItem.Set(rectScrollInner.x, rectItem.y + 3, rectItem.width, fItemHeight);
            }
            fScrollInnerHeight = rectItem.y + rectItem.height + fMargin;
            //fScrollInnerHeight = fFontSize * 40;
            GUI.enabled = true;
            GUI.EndScrollView();

            rectItem.Set(rectInner.x, rectScrollView.y + rectScrollView.height + fMargin * 2, fFontSize * 4, fItemHeight);
            if (GUI.Button(rectItem, "追加", gsButton))
            {
                iConfirmRemove = -1;
                AddSlider();
                sMessageLabel = "追加完了";
                iMessageLabelTimer = 120;
            }

            if (iMessageLabelTimer >= 0)
            {
                rectItem.Set(rectItem.x + rectItem.width + fFontSize, rectItem.y, fFontSize * 5, fItemHeight);
                GUI.Label(rectItem, sMessageLabel, gsLabel);
                iMessageLabelTimer--;
            }

            rectItem.Set(rectWin.width - fFontSize * 12.5f, rectItem.y, fFontSize * 6, fItemHeight);
            if (GUI.Button(rectItem, "設定復元", gsButton))
            {
                LoadXML();
                mm.bUpdate = true;
                sMessageLabel = "복원완료";
                iMessageLabelTimer = 120;
            }

            rectItem.Set(rectWin.width - fFontSize * 6.5f, rectItem.y, fFontSize * 6, fItemHeight);
            if (GUI.Button(rectItem, "設定保存", gsButton))
            {
                SaveXML();
                sMessageLabel = "保存完了";
                iMessageLabelTimer = 120;
            }

            rectItem.Set(rectInner.x, rectItem.y + rectItem.height, fFontSize * 6, fItemHeight);
            string sFillterName;
            if(iFillterNo == (int)eFillter.ALL) sFillterName = "全部表示";
            else if(iFillterNo == (int)eFillter.NORM) sFillterName = "通常表示";
            else if(iFillterNo == (int)eFillter.ID) sFillterName = " ID表示 ";
            else /* if(iFillterNo == (int)eFillter.NAME) */ sFillterName = "名前表示";
            if (GUI.Button(rectItem, sFillterName, gsButton))
            {
                iFillterNo++;
                if(iFillterNo == (int)eFillter.BANPEI) iFillterNo = (int)eFillter.ALL;
            }
            if(iFillterNo == (int)eFillter.NAME){
                rectItem.Set(rectInner.x +  fFontSize * 6 , rectItem.y , fFontSize * 2, fItemHeight);
                if (GUI.Button(rectItem, "<", gsButton))
                {
                    if (--imaidFillterAssign < 0)
                        imaidFillterAssign = mm.listMaid.Count - 1;
                }
                rectItem.x += rectItem.width;
                if (GUI.Button(rectItem, ">", gsButton))
                {
                    if (++imaidFillterAssign >= mm.listMaid.Count)
                        imaidFillterAssign = 0;
                }
                rectItem.Set(rectItem.x + rectItem.width, rectItem.y, rectInner.width - fFontSize * 10, fItemHeight);
                GUI.Label(rectItem, mm.listName[imaidFillterAssign], gsLabel);
            }

            if (iConfirmRemove >= 0)
            {
                rectItem.Set(rectInner.x, rectItem.y + rectItem.height + fMargin, rectInner.width, fItemHeight * 2);
                GUI.Label(rectItem, dm[iConfirmRemove].name + "\n을 삭제 하시겠습니까?");

                rectItem.Set(rectInner.width - fFontSize * 8, rectItem.y + rectItem.height + fMargin, fFontSize * 4, fItemHeight);
                if (GUI.Button(rectItem, "はい", gsButton))
                {
                    RemoveSlider(iConfirmRemove);
                    iConfirmRemove = -1;
                    sMessageLabel = "삭제 완료";
                    iMessageLabelTimer = 120;
                }

                rectItem.Set(rectInner.width - rectItem.width, rectItem.y, rectItem.width, fItemHeight);
                if (GUI.Button(rectItem, "いいえ", gsButton))
                {
                    iConfirmRemove = -1;
                }
            }

            fWinHeight = rectItem.y + rectItem.height + fMargin;

            if (GUI.changed)
            {
                OnGuiChange(iDisable);
            }
            GUI.DragWindow();
            ChkMouseClick();
        }

        private void GuiFuncPopUp(int winId)
        {
            GUIStyle gsSelectionGrid = new GUIStyle();
            gsSelectionGrid.fontSize = Utl.GetPix(12);

            GUIStyleState gssWhite = new GUIStyleState();
            gssWhite.textColor = Color.white;
            gssWhite.background = Texture2D.blackTexture;
            GUIStyleState gssBlack = new GUIStyleState();
            gssBlack.textColor = Color.black;
            gssBlack.background = Texture2D.whiteTexture;

            gsSelectionGrid.normal = gssWhite;
            gsSelectionGrid.hover = gssBlack;

            Rect rectItem = new Rect(0, 0, rectPopUp.width, rectPopUp.height);
            int iTmp = -1;
            iTmp = GUI.SelectionGrid(rectItem, -1, sButtonText, 1, gsSelectionGrid);
            if (iTmp >= 0)
            {
                iReturnPopUp = iTmp;
                if (iActivePopUp >= 0 && dm.Count > iActivePopUp)
                {
                    dm[iActivePopUp].ChangeAnimeType((DataManager.AnimeType)iReturnPopUp);
                }
                iActivePopUp = -1;
                bGuiPopUp = false;
            }
        }
        private bool bFade;
        public void Update()
        {
            if (!bEnablePlugin)
                return;

            if (hotkey.GetKeyState())
            {
                bGui = !bGui;
            }

            if (GameMain.Instance.MainCamera.IsFadeProc())
            {
                bFade = true;
            }

            if (bFade)
            {
                if(!GameMain.Instance.MainCamera.IsFadeProc())
                {
                    bFade = false;
                    mm.bUpdate = true;
                }
            }

            if (mm.bUpdate)
            {
                if (mm.Find())
                {
                    OnMMUpdate();
                    OnGuiChange(-1);
                }
            }

            if (mm.listMaid.Count == 0)
                return;

            for (int i = 0; i < mm.listMaid.Count; i++)
            {
                if (!MaidMgr.IsValid(mm.listMaid[i]))
                {
                    mm.bUpdate = true;
                    return;
                }
            }

            if(isDance && !isDanceInit)
            {
                Maid maid0 = GameMain.Instance.CharacterMgr.GetMaid(0);
                if(maid0 == null || maid0.IsBusy) return;
                isDanceInit = true;
                LoadXML();
                mm.bUpdate = true;
            }

            if (bOnLoad)
            {
                OnLoad();
                gm.MakeDict();
                bOnLoad = false;
            }

            Animate();
            Animate_Group();

            if (bEditSceneEnd)
                return;
            if (bEditScene && !UICamera.InputEnable)
            {
                bEditSceneEnd = true;
                return;
            }

            foreach (Maid m in mm.listMaid)
            {
                if (!m.boMabataki)
                    m.body0.Face.morph.FixBlendValues_Face();
            }
        }

        //

        private void SaveXML()
        {
            List<string> sAddTagList = new List<string>();
            List<Dictionary<string, string>> data = new List<Dictionary<string, string>>();
            foreach (DataManager d in dm)
            {
                if (string.IsNullOrEmpty(d.tag))
                    continue;

                data.Add(new Dictionary<string, string>());
                data.Last().Add("name", d.name);
                data.Last().Add("tag", d.tag);
                data.Last().Add("val", d.val.ToString());
                if (d.enable)
                    data.Last().Add("enable", d.enable.ToString());
                else
                {
                    if (d.enableClick)
                        data.Last().Add("enable", d.enable.ToString());
                    else
                        data.Last().Add("enable", d.enableInSavedata.ToString());
                }
                data.Last().Add("fold", d.fold.ToString());
                data.Last().Add("animetype", ((int)d.animeType).ToString());
                data.Last().Add("modulate1", d.modulate1.ToString());
                data.Last().Add("modulate2", d.modulate2.ToString());
                data.Last().Add("point", d.point.ToString());
                data.Last().Add("actiontimemin", d.actionTimeMin.ToString());
                data.Last().Add("actiontimemax", d.actionTimeMax.ToString());
                data.Last().Add("actionintervalmin", d.actionIntervalMin.ToString());
                data.Last().Add("actionintervalmax", d.actionIntervalMax.ToString());
                data.Last().Add("timerwaittype", ((int)d.timerWaitType).ToString());
                if (d.enable)
                    data.Last().Add("maid", d.maid.ToString());
                else
                    data.Last().Add("maid", d.maidInSavedata.ToString());
                data.Last().Add("maidguid", d.maidGuid);
                data.Last().Add("maidnamebyguid", d.maidNameByGuid);
                data.Last().Add("group", d.group.ToString());
                data.Last().Add("groupmaster", d.groupMaster.ToString());
                data.Last().Add("groupreverse", d.groupReverse.ToString());
                data.Last().Add("groupoffset", d.groupOffset.ToString());
                data.Last().Add("groupmodulate2", d.groupModulate2.ToString());
                data.Last().Add("grouppoint", d.groupPoint.ToString());
                sAddTagList.Add(d.tag);
            }
            xml.SetVal(data);
            xml.SaveXML();
        }

        private void LoadXML()
        {
            xml.LoadXML();

            {
                string sTmp = xml.GetString("keyshowpanel");
                if (sTmp != string.Empty)
                    hotkey = new ShortCutKey(sTmp);
                sTmp = xml.GetString("sliderRange");
                if (sTmp != string.Empty)
                    sliderRange = float.Parse(sTmp);
            }

            dm = new List<DataManager>();
            gm = new GroupMgr(dm);

            List<Dictionary<string, string>> data = xml.GetAttr();

            foreach (Dictionary<string, string> dict in data)
            {
                string name = string.Empty;
                string tag = string.Empty;
                float val = 0f;
                bool enable = false;
                bool enableInSavedata = false;
                bool fold = false;
                int animetype = 0;
                float modulate1 = 0f;
                float modulate2 = 0f;
                float point = 0f;
                int actiontimemin = 0;
                int actiontimemax = 0;
                int actionintervalmin = 0;
                int actionintervalmax = 0;
                int timerwaittype = 0;
                int maid = 0;
                int maidInSavedata = 0;
                string maidGuid = string.Empty;
                string maidNameByGuid = string.Empty;
                int group = -1;
                bool groupmaster = false;
                bool groupReverse = false;
                float groupOffset = 0f;
                float groupModulate2 = 1f;
                float groupPoint = 0f;

                foreach (KeyValuePair<string, string> kvp in dict)
                {
                    switch (kvp.Key)
                    {
                        case "name":
                            name = kvp.Value;
                            break;
                        case "tag":
                            tag = kvp.Value;
                            break;
                        case "val":
                            float.TryParse(kvp.Value, out val);
                            break;
                        case "enable":
                            bool.TryParse(kvp.Value, out enableInSavedata);
                            break;
                        case "fold":
                            bool.TryParse(kvp.Value, out fold);
                            break;
                        case "animetype":
                            int.TryParse(kvp.Value, out animetype);
                            break;
                        case "modulate1":
                            float.TryParse(kvp.Value, out modulate1);
                            break;
                        case "modulate2":
                            float.TryParse(kvp.Value, out modulate2);
                            break;
                        case "point":
                            float.TryParse(kvp.Value, out point);
                            if (point > 1f)
                                point = 1f;
                            break;
                        case "actiontimemin":
                            int.TryParse(kvp.Value, out actiontimemin);
                            break;
                        case "actiontimemax":
                            int.TryParse(kvp.Value, out actiontimemax);
                            break;
                        case "actionintervalmin":
                            int.TryParse(kvp.Value, out actionintervalmin);
                            break;
                        case "actionintervalmax":
                            int.TryParse(kvp.Value, out actionintervalmax);
                            break;
                        case "timerwaittype":
                            int.TryParse(kvp.Value, out timerwaittype);
                            break;
                        case "maid":
                            int.TryParse(kvp.Value, out maidInSavedata);
                            break;
                        case "maidguid":
                            maidGuid = kvp.Value;
                            break;
                        case "maidnamebyguid":
                            maidNameByGuid = kvp.Value;
                            break;
                        case "group":
                            int.TryParse(kvp.Value, out group);
                            break;
                        case "groupmaster":
                            bool.TryParse(kvp.Value, out groupmaster);
                            break;
                        case "groupreverse":
                            bool.TryParse(kvp.Value, out groupReverse);
                            break;
                        case "groupoffset":
                            float.TryParse(kvp.Value, out groupOffset);
                            break;
                        case "groupmodulate2":
                            float.TryParse(kvp.Value, out groupModulate2);
                            break;
                        case "grouppoint":
                            float.TryParse(kvp.Value, out groupPoint);
                            break;
                    }
                }

                if (string.IsNullOrEmpty(tag))
                    continue;

                //if (maidInSavedata < 0)
                //    maidInSavedata = 0;
                //if (maidInSavedata > mm.listMaid.Count - 1)
                //{
                //    maid = 0;
                //    enable = false;
                //}
                //else
                //{
                //    maid = maidInSavedata;
                //    enable = enableInSavedata;
                //}

                dm.Add(new DataManager(name, tag, val,
                        enable, enableInSavedata, fold,
                        (DataManager.AnimeType)animetype, modulate1, modulate2, point,
                        actiontimemin, actiontimemax, actionintervalmin, actionintervalmax,
                        (DataManager.TimerWaitType)timerwaittype, maid, maidInSavedata,
                         maidGuid, maidNameByGuid,
                        group, groupmaster,
                        groupReverse, groupOffset, groupModulate2, groupPoint
                        ));
            }

            bOnLoad = true;
        }

        private void AddSlider()
        {
            dm.Add(new DataManager(string.Empty, string.Empty, 0f));
        }

        private void RemoveSlider(int i)
        {
            dm.RemoveAt(i);
        }

        private void OnGuiChange(int iDisable)
        {
            if (mm.listMaid.Count == 0)
                return;

            bool[] bFace = new bool[mm.listMaid.Count];
            bool[] b = new bool[mm.listMaid.Count];


            for (int i = 0; i < dm.Count; i++)
            {
                if (dm[i].mod == DataManager.ModType.none)
                {
                    if (dm[i].maid < 0)
                    continue;

                    if (!dm[i].enable)
                    {
                        if (iDisable == i)
                            VertexMorph_FromProcItem(mm.listMaid[dm[i].maid].body0, dm[i].tag, 0f);
                        continue;
                    }
                    b[dm[i].maid] = VertexMorph_FromProcItem(mm.listMaid[dm[i].maid].body0, dm[i].tag, dm[i].val);
                    bFace[dm[i].maid] = b[dm[i].maid] ? b[dm[i].maid] : bFace[dm[i].maid];
                }
                else
                {
                    for (int j = 0; j < bFace.Length; j++)
                    {
                        if (!dm[i].enable)
                        {
                            if (iDisable == i)
                                VertexMorph_FromProcItem(mm.listMaid[j].body0, dm[i].tag, 0f);
                            continue;
                        }
                        b[j] = VertexMorph_FromProcItem(mm.listMaid[j].body0, dm[i].tag, dm[i].val);
                        bFace[j] = b[j] ? b[j] : bFace[j];
                    }
                }

            }

            for (int i = 0; i < bFace.Length; i++)
            {
                if (bFace[i] == mm.listMaid[i].boMabataki)
                {
                    mm.listMaid[i].boMabataki = !bFace[i];
                    if (bFace[i])
                    {                        
                        mm.listMaid[i].body0.Face.morph.EyeMabataki = 0f; //  윙크
                    }
                }
            }

            //for (int i = 0; i < dm.Count; i++)
            //{
            //    if (dm[i].enable)
            //    {
            //        b[dm[i].maid] = VertexMorph_FromProcItem(mm.listMaid[dm[i].maid].body0, dm[i].tag, dm[i].val);
            //        //if (iChangeMaid >= 0)
            //        //{
            //        //    if (mm.listMaid.Count > iChangeMaid)
            //        //        VertexMorph_FromProcItem(mm.listMaid[iChangeMaid].body0, dm[i].tag, 0f, true);
            //        //}
            //        //b[dm[i].maid] = VertexMorph_FromProcItem(mm.listMaid[dm[i].maid].body0, dm[i].tag, dm[i].val, true);
            //    }
            //    else if (i == iDisable)
            //    {
            //        VertexMorph_FromProcItem(mm.listMaid[dm[i].maid].body0, dm[i].tag, dm[i].val);
            //        b[dm[i].maid] = false;
            //    }
            //    bFace[dm[i].maid] = b[dm[i].maid] ? b[dm[i].maid] : bFace[dm[i].maid];
            //}

            //for (int i = 0; i < bFace.Length; i++)
            //{
            //    if (bFace[i] == mm.listMaid[i].boMabataki)
            //    {
            //        mm.listMaid[i].boMabataki = !bFace[i];
            //        if (bFace[i])
            //        {
            //            //  윙크
            //            mm.listMaid[i].body0.Face.morph.EyeMabataki = 0f;
            //            // 目閉じ対策？　とりあえず保留 눈 닫 대책? 일단 보류
            //            //                        mm.listMaid[i].body0.Face.morph.BlendValues[(int)mm.listMaid[i].body0.Face.morph.hash["eyeclose"]] = 0f;
            //        }
            //    }
            //}
        }

        private void OnMMUpdate()
        {
            int iMaidCount = mm.listMaid.Count;
            for (int i = 0; i < dm.Count; i++)
            {
                if (dm[i].maidSel >= iMaidCount)
                    dm[i].maidSel = 0;
                ResetMaidAssign(i);
            }
        }

        private void ResetMaidAssign(int i)
        {
            if (dm[i].maidGuid != string.Empty)
            {
                for (int n = 0; n < mm.listName.Count; n++)
                {
//                    if (mm.listMaid[n].Param.status.guid == dm[i].maidGuid)
                    if (mm.listMaid[n].status.guid == dm[i].maidGuid)                    {
                        dm[i].maid = n;
                        dm[i].maidFixedAssign = n;
                        dm[i].maidNameByGuid = mm.listName[n];
                        return;
                    }
                }
                dm[i].maid = -1;
                dm[i].maidFixedAssign = -1;
                return;
            }

            Match match = regexNameAssign.Match(dm[i].name);
            if (match.Success)
            {
                string sMatch = match.Value.Replace("*", string.Empty);
                for (int n = 0; n < mm.listMaid.Count; n++)
                {
                    string sTmp = mm.listName[n].Replace(" ", string.Empty);
                    if (sTmp == sMatch)
                    {
                        dm[i].maid = n;
                        dm[i].maidFixedAssign = n;
                        return;
                    }
                }
                dm[i].maid = -1;
                dm[i].maidFixedAssign = -1;
                return;
            }
            dm[i].maid = dm[i].maidSel;
            dm[i].maidFixedAssign = -1;
        }

        private void OnNameFieldChange(int i)
        {
            Match match = regexNameAssign.Match(dm[i].name);
            if (match.Success)
            {
                string sMatch = match.Value.Replace("*", string.Empty);
                for (int n = 0; n < mm.listName.Count; n++)
                {
                    string sTmp = mm.listName[n].Replace(" ", string.Empty);
                    if (sTmp == sMatch)
                    {
                        dm[i].maidFixedAssign = n;
                        dm[i].maid = n;
                        dm[i].maidGuid = string.Empty;
                        dm[i].maidNameByGuid = string.Empty;
                        return;
                    }
                }
                dm[i].maidFixedAssign = -1;
                dm[i].maid = -1;
                return;
            }
            dm[i].maidFixedAssign = -1;
            dm[i].maid = dm[i].maidSel;
        }

        private void OnClickMaidAssignButton(int i)
        {
            if (dm[i].maidGuid != string.Empty)
            {
                dm[i].maidGuid = string.Empty;
                dm[i].maidNameByGuid = string.Empty;
                dm[i].maidFixedAssign = -1;
                dm[i].maid = dm[i].maidSel;
                return;
            }

            if (dm[i].maidFixedAssign >= 0)
            {
                SetNameFieldMaidAssign(i, false);
//                dm[i].maidGuid = mm.listMaid[dm[i].maidSel].Param.status.guid;
                dm[i].maidGuid = mm.listMaid[dm[i].maidSel].status.guid;
                dm[i].maidNameByGuid = mm.listName[dm[i].maidSel];
                dm[i].maidFixedAssign = dm[i].maidSel;
                dm[i].maid = dm[i].maidSel;
                return;
            }

            SetNameFieldMaidAssign(i, true);
            dm[i].maidGuid = string.Empty;
            dm[i].maidNameByGuid = string.Empty;
            dm[i].maidFixedAssign = dm[i].maidSel;
            dm[i].maid = dm[i].maidSel;
        }

        private void SetNameFieldMaidAssign(int i, bool b)
        {
            Match match = regexNameAssign.Match(dm[i].name);
            if (b)
            {
                string sMaidName = mm.listName[dm[i].maidSel];
                sMaidName = "*" + sMaidName.Replace(" ", string.Empty) + "*";

                if (match.Success)
                {
                    dm[i].name = regexNameAssign.Replace(dm[i].name, sMaidName);
                }
                else
                {
                    dm[i].name = sMaidName + dm[i].name;
                }
            }
            else
            {
                if (match.Success)
                {
                    dm[i].name = regexNameAssign.Replace(dm[i].name, string.Empty);
                }
            }
        }

        private void MoveDM(int i, bool bUp)
        {
            if ((i == 0 && bUp) || i == dm.Count - 1 && !bUp)
                return;

            int iTarget = bUp ? i - 1 : i + 1;

            DataManager dmTmp = (DataManager)dm[iTarget].Clone();
            dm[iTarget] = (DataManager)dm[i].Clone();
            dm[i] = dmTmp;
        }

        //

        private void OnLoad()
        {
            List<HashSet<string>> listEnableTag = new List<HashSet<string>>();
            for (int i = 0; i < mm.listMaid.Count; i++)
            {
                listEnableTag.Add(new HashSet<string>());
            }
            bool bHit = false;
            for (int i = 0; i < dm.Count; i++)
            {
                bHit = false;
                if (dm[i].maidGuid != string.Empty)
                {
                    for (int n = 0; n < mm.listName.Count; n++)
                    {
///CM>COM               if (mm.listMaid[n].Param.status.guid == dm[i].maidGuid)
                        if (mm.listMaid[n].status.guid == dm[i].maidGuid)
                        {
                            if (dm[i].enableInSavedata && IsValidKey(mm.listMaid[n].body0, dm[i].tag) && listEnableTag[n].Add(dm[i].tag))
                            {
                                dm[i].enable = true;
                                bHit = true;
                                break;
                            }
                        }
                    }
                    if (!bHit)
                        dm[i].enable = false;
                    continue;
                }

                Match match = regexNameAssign.Match(dm[i].name);
                if (match.Success)
                {
                    string sMatch = match.Value.Replace("*", string.Empty);
                    for (int n = 0; n < mm.listMaid.Count; n++)
                    {
                        string sTmp = mm.listName[n].Replace(" ", string.Empty);
                        if (sTmp == sMatch)
                        {
                            if (dm[i].enableInSavedata && IsValidKey(mm.listMaid[n].body0, dm[i].tag) && listEnableTag[n].Add(dm[i].tag))
                            {
                                dm[i].enable = true;
                                bHit = true;
                                break;
                            }
                        }
                    }
                    if (!bHit)
                        dm[i].enable = false;
                    continue;
                }

            }

            for (int i = 0; i < dm.Count; i++)
            {
                Match match = regexNameAssign.Match(dm[i].name);
                if (dm[i].maidGuid != string.Empty || match.Success)
                    continue;

                if (dm[i].maidInSavedata >= mm.listMaid.Count || dm[i].maidInSavedata < 0)
                {
                    dm[i].maid = 0;
                    dm[i].maidSel = 0;
                }
                else
                {
                    dm[i].maid = dm[i].maidInSavedata;
                    dm[i].maidSel = dm[i].maidInSavedata;
                }

                if (dm[i].enableInSavedata && IsValidKey(mm.listMaid[dm[i].maid].body0, dm[i].tag) && listEnableTag[dm[i].maid].Add(dm[i].tag))
                    dm[i].enable = true;
                else
                    dm[i].enable = false;
            }
        }

        private void Animate()
        {
            for (int i = 0; i < dm.Count; i++)
            {
                if (!dm[i].enable)
                    continue;

                if (dm[i].mod==DataManager.ModType.none)
                {
                    if (dm[i].maid < 0 || string.IsNullOrEmpty(dm[i].tag))
                        continue;
                }

                if (gm.IsSkipAnimate(i))
                {
                    dm[i].bAnimateGroup = true;
                    continue;
                }

                if (dm[i].animeType == DataManager.AnimeType.none)
                    continue;


                if (dm[i].actionTimeMax != 0 && dm[i].actionIntervalMax != 0)
                {
                    if (--dm[i].actionTimer < 0)
                    {
                        if (dm[i].actionTimer < -dm[i].actionTime)
                        {
                            dm[i].actionInterval = UnityEngine.Random.Range(dm[i].actionIntervalMin, dm[i].actionIntervalMax);
                            dm[i].actionTime = UnityEngine.Random.Range(dm[i].actionTimeMin, dm[i].actionTimeMax);

                            dm[i].actionTimer = dm[i].actionInterval;
                            switch (dm[i].timerWaitType)
                            {
                                case DataManager.TimerWaitType.zero:
                                    dm[i].val = 0f;
                                    break;
                                case DataManager.TimerWaitType.point:
                                    dm[i].val = dm[i].point;
                                    break;
                            }
                            continue;
                        }
                    }
                    else
                    {
                        continue;
                    }
                }

                switch (dm[i].animeType)
                {
                    case DataManager.AnimeType.increase:
                        dm[i].val = DataIncrease(dm[i]);
                        break;
                    case DataManager.AnimeType.decrease:
                        dm[i].val = DataDecrease(dm[i]);
                        break;
                    case DataManager.AnimeType.repeat:
                        dm[i].val = DataRepetition(dm[i]);
                        break;
                    case DataManager.AnimeType.random:
                        dm[i] = DataRandom(dm[i]);
                        break;
                }

                if (dm[i].mod == DataManager.ModType.none)
                {
                    VertexMorph_FromProcItem(mm.listMaid[dm[i].maid].body0, dm[i].tag, dm[i].val);
                }
                else
                {
                    VertexMorph_FromProcItemAll(i);
                }
            }
        }

        private void VertexMorph_FromProcItemAll(int i)
        {
            foreach (var md in mm.listMaid)
            {
                VertexMorph_FromProcItem(md.body0, dm[i].tag, dm[i].val);
            }
        }

        private void Animate_Group()
        {
            for (int i = 0; i < dm.Count; i++)
            {
                if (dm[i].bAnimateGroup)
                {
                    dm[i].val = GetGroupVal(i);
                    if (dm[i].mod == DataManager.ModType.none)
                    {
                        VertexMorph_FromProcItem(mm.listMaid[dm[i].maid].body0, dm[i].tag, dm[i].val);                        
                    }
                    else
                    {
                        VertexMorph_FromProcItemAll(i);
                    }
                    dm[i].bAnimateGroup = false;
                }
            }
        }

        private float GetGroupVal(int iDm)
        {
            if (dm[gm.GetMaster(dm[iDm].group)].modulate2 == 0f)
                return dm[iDm].point;

            float fMin, fMax, fTime;

            GetMinMax(dm[gm.GetMaster(dm[iDm].group)].point, dm[gm.GetMaster(dm[iDm].group)].modulate2, out fMin, out fMax);
            fTime = (dm[gm.GetMaster(dm[iDm].group)].val - fMin) / (fMax - fMin);
            
            if (dm[iDm].groupReverse)
                fTime = 1f - fTime;
            GetMinMax(dm[iDm].groupPoint, dm[iDm].groupModulate2, out fMin, out fMax);

            if (dm[gm.GetMaster(dm[iDm].group)].animeType == DataManager.AnimeType.repeat)
                fTime = Mathf.PingPong(fTime + dm[iDm].groupOffset * dm[gm.GetMaster(dm[iDm].group)].process, 1f);
            else
                fTime = Mathf.Repeat(fTime + dm[iDm].groupOffset, 1f);

            return (fTime * dm[iDm].groupModulate2) + fMin;
        }

        private float DataIncrease(DataManager d)
        {
            float fMin;
            float fMax;
            float f;

            GetMinMax(d.point, d.modulate2, out fMin, out fMax);

            if (fMin >= fMax)
                return fMax;

            f = d.val + (d.modulate2 * d.modulate1) * 0.75f;
            while (f > fMax)
            {
                f = fMin + (f - fMax);
            }

            return f;
        }

        private float DataDecrease(DataManager d)
        {
            float fMin;
            float fMax;
            float f;

            GetMinMax(d.point, d.modulate2, out fMin, out fMax);
            if (fMin >= fMax)
                return fMax;

            f = d.val - (d.modulate2 * d.modulate1) * 0.75f;
            while (f < fMin)
            {
                f = fMax + (f - fMin);
            }

            return f;
        }

        private float DataRepetition(DataManager d)
        {
            float fMin;
            float fMax;
            float f;

            GetMinMax(d.point, d.modulate2, out fMin, out fMax);

            f = d.val + (d.modulate2 * d.modulate1) * 0.75f * d.process;
            while (f > fMax || f < fMin)
            {
                if (d.process >= 0)
                    f -= f - fMax;
                else
                    f -= f - fMin;
                d.process *= -1;
            }

            return f;
        }

        private DataManager DataRandom(DataManager d)
        {
            if (--d.process < 0)
            {
                float fMin;
                float fMax;
                GetMinMax(d.point, d.modulate2, out fMin, out fMax);

                d.val = UnityEngine.Random.Range(fMin, fMax);
                d.process = 20 - (int)(d.modulate1 * 20);
            }
            return d;
        }

        private void GetMinMax(float fp, float fm, out float fMin, out float fMax)
        {
            fMin = (1f - fm) * fp;
            fMax = fMin + fm;
        }

        /// <summary>
        /// 얼굴 효과 반영후 얼굴 여부 반환?
        /// </summary>
        /// <param name="body"></param>
        /// <param name="sTag"></param>
        /// <param name="f"></param>
        /// <returns></returns>
        private bool VertexMorph_FromProcItem(TBody body, string sTag, float f)
        {
            bool bFace = false;
            for (int i = 0; i < body.goSlot.Count; i++)
            {
                TMorph morph = body.goSlot[i].morph;
                if (morph != null)
                {
                    if (morph.Contains(sTag))
                    {
                        if (i == 1)
                        {
                            bFace = true;
                        }
		                int h = (int)morph.hash[sTag];
                        ///cm>com
                        ///不要な処理につきコメントアウト 불필요한 처리에 대해 주석
                        ///                     morph.BlendValuesCHK[h] = -1f;

                        ///cm>com               morph.BlendValues[h] = f;
                        morph.SetBlendValues(h, f);

                    	morph.FixBlendValues();
                    }
                }
            }
            return bFace;
        }

		//[2018/11/21 @usausaex]キーの並び替えのために変更;
        private string[] GetAllKeys(TBody body)
        {
            List<string> listKeys = new List<string>();
            for(int i = 0; i < body.goSlot.Count; i++) {
                List<string> listSubKeys = new List<string>();
                TMorph morph = body.goSlot[i].morph;
                
                if(morph != null) {
                    foreach(string s in morph.hash.Keys) {
                        if(listKeys.Contains(s)) {
                            continue;
                        }
                        if(listSubKeys.Contains(s)) {
                            continue;
                        }
                        if(sIgnoreKeys.Contains(s)) {
                            continue;
                        }
                        listSubKeys.Add(s);
                    }
                }
                //アイテムごとのキーlistSubKeysをソートして最終リストlistKeysに追加;
                listSubKeys.Sort();
                listKeys.AddRange(listSubKeys);
            }
            
            return listKeys.ToArray();
        }

        private bool IsValidKey(TBody body, string sTag)
        {
            for (int i = 0; i < body.goSlot.Count; i++)
            {
                TMorph morph = body.goSlot[i].morph;
                if (morph != null)
                {
                    if (morph.Contains(sTag))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        //

        private class NumericInputWindow
        {
            public readonly int WINDOW_ID;

            public Rect rect { get; set; }
            private float fMargin { get; set; }
            private float fLeftPos { get; set; }

            public bool show { get; private set; }

            public Action<float[]> func { get; private set; }

            private GUIStyle gsLabel { get; set; }
            private GUIStyle gsButton { get; set; }
            private GUIStyle gsText { get; set; }

            private bool bGroup { get; set; }
            private float[] fVals { get; set; }
            private string[] sVals { get; set; }
            private static readonly string[] sLabels;
            public int iDmNumber { get; set; }

            public NumericInputWindow(int iWIndowID)
            {
                WINDOW_ID = iWIndowID;
            }

            static NumericInputWindow()
            {
                sLabels = new string[3] { "速さ", "幅", "基準点" };
            }

            public void Set(Vector2 p, float fWidth, float fParentWidth, int iFontSize, float[] fVals, int iDm, bool bGroup, Action<float[]> f)
            {
                rect = new Rect(p.x, p.y, fWidth, 0f);
                fLeftPos = p.x - fParentWidth - fWidth;

                gsLabel = new GUIStyle("label");
                gsLabel.fontSize = iFontSize;
                gsLabel.alignment = TextAnchor.MiddleLeft;

                gsButton = new GUIStyle("button");
                gsButton.fontSize = iFontSize;
                gsButton.alignment = TextAnchor.MiddleCenter;

                gsText = new GUIStyle("textfield");
                gsText.fontSize = iFontSize;
                gsText.alignment = TextAnchor.UpperLeft;

                fMargin = iFontSize * 0.3f;

                func = f;

                this.fVals = fVals;
                sVals = new string[fVals.Length];
                for(int i = 0; i < sVals.Length; i++)
                {
                    sVals[i] = fVals[i].ToString();
                }

                iDmNumber = iDm;
                this.bGroup = bGroup;

                show = true;
            }

            public void GuiFunc(int winId)
            {
                int iFontSize = gsLabel.fontSize;
                Rect rectItem = new Rect(iFontSize * 0.5f, iFontSize * 0.5f, (rect.width - iFontSize) / 2f, iFontSize * 1.5f);

                for (int i = 0; i < fVals.Length; i++)
                {
                    string sTmp;
                    //rectItem.width = (rect.width - iFontSize) / 5f * 2f;
                    GUI.Label(rectItem, bGroup && i == 0 ? "オフセット" : sLabels[i], gsLabel);

                    rectItem.x += rectItem.width;
                    //rectItem.width = (rect.width - iFontSize) / 5f * 3f;
                    sTmp = Utl.DrawTextFieldF(rectItem, sVals[i], bGroup && i == 0 ? -1f : 0f, 1f, gsText);
                    if(sTmp != sVals[i])
                    {
                        sVals[i] = sTmp;
                        fVals[i] = float.Parse(sTmp);
                    }

                    rectItem.y += rectItem.height;
                    rectItem.x = iFontSize * 0.5f;
                }

                float fHeight = rectItem.y + fMargin;
                if (rect.height != fHeight)
                {
                    Rect rectTmp = new Rect(rect.x, rect.y, rect.width, fHeight);
                    rect = rectTmp;
                }
                if (rect.x + rect.width > Screen.width)
                {
                    Rect rectTmp = new Rect(fLeftPos, rect.y, rect.width, rect.height);
                    rect = rectTmp;
                }
                //else if (rect.y < 0f)
                //{
                //    Rect rectTmp = new Rect(rect.x, fUpPos, rect.width, rect.height);
                //    rect = rectTmp;
                //}

                if (GUI.changed)
                {
                    func(fVals);
                }

                GUI.DragWindow();

                if (GetAnyMouseButtonDown())
                {
                    Vector2 v2Tmp = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
                    if (!rect.Contains(v2Tmp))
                    {
                        func(fVals);
                        show = false;
                    }
                }
            }

            private bool GetAnyMouseButtonDown()
            {
                return Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2);
            }

        }

        private class ComboBox
        {
            public readonly int WINDOW_ID;

            public Rect rect { get; set; }
            private Rect rectItem { get; set; }
            public bool show { get; set; }
            private string[] sItems { get; set; }

            private GUIStyle gsSelectionGrid { get; set; }
            private GUIStyleState gssBlack { get; set; }
            private GUIStyleState gssWhite { get; set; }

            public Action<int> func { get; private set; }

            private bool bScroll { get; set; }
            private Vector2 v2Scroll;

            public ComboBox(int iWIndowID)
            {
                WINDOW_ID = iWIndowID;
            }

            public void Set(Rect r, string[] s, int i, Action<int> f)
            {
                if (r.height > Screen.height * 0.5f)
                {
                    rect = new Rect(r.x, r.y, r.width, Screen.height * 0.5f);
                    bScroll = true;
                }
                else
                {
                    bScroll = false;
                    rect = r;
                }

                //rect = r;

                sItems = s;

                gsSelectionGrid = new GUIStyle();
                gsSelectionGrid.fontSize = i;

                gssBlack = new GUIStyleState();
                gssBlack.textColor = Color.white;
                gssBlack.background = Texture2D.blackTexture;

                gssWhite = new GUIStyleState();
                gssWhite.textColor = Color.black;
                gssWhite.background = Texture2D.whiteTexture;

                gsSelectionGrid.normal = gssBlack;
                gsSelectionGrid.hover = gssWhite;

                rectItem = new Rect(0f, 0f, r.width, r.height);

                func = f;

                show = true;
            }

            public void GuiFunc(int winId)
            {
                int iTmp = -1;
                if (bScroll)
                    v2Scroll = GUI.BeginScrollView(new Rect(0f, 0f, rect.width, rect.height), v2Scroll, rectItem);

                iTmp = GUI.SelectionGrid(rectItem, -1, sItems, 1, gsSelectionGrid);
                if (bScroll)
                    GUI.EndScrollView();

                if (iTmp >= 0)
                {
                    func(iTmp);
                    show = false;
                }

                if (GetAnyMouseButtonDown())
                {
                    Vector2 v2Tmp = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
                    if (!rect.Contains(v2Tmp))
                        show = false;
                }
            }

            private int GetPix(int i)
            {
                float f = 1f + (Screen.width / 1280f - 1f) * 0.6f;
                return (int)(f * i);
            }

            private bool GetAnyMouseButtonDown()
            {
                return Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2);
            }
        }

        private class DataManager : ICloneable
        {
            /// <summary>
            /// 반복 타입
            /// </summary>
            public enum AnimeType
            {
                none,
                increase,
                decrease,
                repeat,
                random
            }
            public enum TimerWaitType
            {
                none,
                zero,
                point
            }


            /// <summary>
            /// 메이드 전체 적용 여부
            /// </summary>
            public enum ModType
            {
                none,
                All
            }

            // 모든 캐릭 적용 여부
            public ModType mod { get; set; }

            // 활성 여부
            public bool enable { get; set; }
            public bool enableInSavedata { get; set; }
            public bool enableClick { get; set; }
            public bool fold { get; set; }

            public string name { get; set; }
            public string tag { get; set; }
            public float val { get; set; }

            public int maid { get; set; }
            public int maidInSavedata { get; set; }
            
            // 메이드 번호?
            public int maidSel { get; set; }
            public int maidFixedAssign { get; set; }
            public string maidGuid { get; set; }
            public string maidNameByGuid { get; set; }

            public AnimeType animeType = AnimeType.none;
            public float modulate1 { get; set; }
            public float modulate2 { get; set; }
            public float point { get; set; }
            public int process { get; set; }

            public bool extraSetting { get; set; }
            public int actionTimer { get; set; }
            public int actionTimeMin { get; set; }
            public int actionTimeMax { get; set; }
            public int actionIntervalMin { get; set; }
            public int actionIntervalMax { get; set; }
            public int actionTime { get; set; }
            public int actionInterval { get; set; }

            public TimerWaitType timerWaitType = TimerWaitType.none;

            public int group { get; set; }
            public bool groupMaster { get; set; }

            public bool groupReverse { get; set; }
            public float groupOffset { get; set; }
            public float groupModulate2 { get; set; }
            public float groupPoint { get; set; }
            
            
            //public string groupPointText { get; set; }
            //public string groupModulate2Text { get; set; }

            public bool bAnimateGroup { get; set; }

            public DataManager(string name, string tag, float val)
            {
                this.name = name;
                this.tag = tag;
                this.val = val;
                enable = true;
                modulate1 = 0.1f;
                modulate2 = 1f;
                process = 1;
                maidFixedAssign = -1;
                maidGuid = string.Empty;
                maidNameByGuid = string.Empty;
                group = -1;

                groupModulate2 = 1f;
                //groupPointText = "0";
                //groupModulate2Text = "1";
            }

            public DataManager(string name, string tag, float val,
                                bool enable, bool enablesave, bool fold, AnimeType anime,
                                float f1, float f2, float p,
                                int atmin, int atmax, int aimin, int aimax,
                                TimerWaitType timer, int maid, int maidsave,
                                string maidGuid, string maidNameByGuid,
                                int group, bool groupMaster,
                                bool groupReverse, float groupOffset, float groupModulate2, float groupPoint
                                )
            {
                this.name = name;
                this.tag = tag;
                this.val = val;
                this.enable = enable;
                this.fold = fold;
                this.enableInSavedata = enablesave;
                this.modulate1 = f1;
                this.modulate2 = f2;
                this.point = p;
                this.actionTimeMin = atmin;
                this.actionTimeMax = atmax;
                this.actionIntervalMin = aimin;
                this.actionIntervalMax = aimax;
                this.timerWaitType = timer;
                this.maid = maid;
                this.maidInSavedata = maidsave;
                this.maidFixedAssign = -1;
                this.maidGuid = maidGuid;
                this.maidNameByGuid = maidNameByGuid;
                this.group = group;
                this.groupMaster = groupMaster;
                this.groupReverse = groupReverse;
                this.groupOffset = groupOffset;
                this.groupModulate2 = groupModulate2;
                this.groupPoint = groupPoint;
                ChangeAnimeType(anime);

                groupModulate2 = 1f;
                //groupPointText = "0";
                //groupModulate2Text = "1";
            }

            public void ChangeAnimeType(AnimeType anime)
            {
                animeType = anime;
                switch (animeType)
                {
                    case AnimeType.increase:
                        process = 1;
                        break;
                    case AnimeType.decrease:
                        process = -1;
                        break;
                    case AnimeType.repeat:
                        process = 1;
                        break;
                    case AnimeType.random:
                        process = 1;
                        break;
                    default:
                        process = 1;
                        break;
                }
            }

            public object Clone()
            {
                return MemberwiseClone();
            }
        }


        private class ShortCutKey
        {
            private string key = string.Empty;
            private bool ctrl;
            private bool alt;
            private bool shift;

            public ShortCutKey()
            {
                key = "f4";
                ctrl = alt = shift = false;
            }

            public ShortCutKey(string s)
            {
                StringToKey(s);
            }

            public bool GetKeyState()
            {
                return
                (ctrl ? (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) : true) &&
                (shift ? (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) : true) &&
                (alt ? (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt)) : true) &&
                Input.GetKeyDown(key);
            }

            private void StringToKey(string s)
            {
                if (string.IsNullOrEmpty(s))
                    return;

                ctrl = alt = shift = false;

                s = s.Replace(" ", string.Empty);
                string[] sSplit = s.Split('+');
                if (sSplit.Length == 1)
                {
                    key = s.Trim().ToLower();
                    return;
                }

                for (int i = 0; i < sSplit.Length - 1; i++)
                {
                    string sTmp = sSplit[i].Trim();
                    if (string.Compare(sTmp, "ctrl", true) == 0)
                    {
                        ctrl = true;
                        continue;
                    }
                    if (string.Compare(sTmp, "shift", true) == 0)
                    {
                        shift = true;
                        continue;
                    }
                    if (string.Compare(sTmp, "alt", true) == 0)
                    {
                        alt = true;
                        continue;
                    }
                }
                key = sSplit[sSplit.Length - 1].Trim().ToLower();
            }
        }

        private class XMLManager
        {
            private string sXmlFileName = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) + @"\Config\ShapeAnimator.xml";
            private XmlDocument xmldoc = new XmlDocument();
            private bool bModify = false;

            public XMLManager()
            {
                Init();
            }

            private void Init()
            {
                if (!File.Exists(sXmlFileName))
                {
                    xmldoc = new XmlDocument();
                    XmlDeclaration declaration = xmldoc.CreateXmlDeclaration("1.0", "UTF-8", null);
                    xmldoc.AppendChild(declaration);

                    XmlElement root = xmldoc.CreateElement("root");
                    XmlElement items = xmldoc.CreateElement("items");
                    XmlElement config = xmldoc.CreateElement("config");

                    XmlElement keyshowpanel = xmldoc.CreateElement("keyshowpanel");
                    keyshowpanel.SetAttribute("val", "f4");

                    config.AppendChild(keyshowpanel);

                    root.AppendChild(items);
                    root.AppendChild(config);
                    xmldoc.AppendChild(root);

                    xmldoc.Save(sXmlFileName);
                }
                xmldoc.Load(sXmlFileName);
            }

            public bool SetVal(string sParentNode, string sNode, string sVal)
            {
                if (xmldoc == null || string.IsNullOrEmpty(sNode))
                    return false;

                XmlNodeList configs = xmldoc.GetElementsByTagName(sParentNode);
                if (configs.Count == 0)
                    return false;

                XmlNode node = configs[0].SelectSingleNode(sNode);
                if (node == null)
                {
                    XmlElement newNode = xmldoc.CreateElement(sNode);
                    newNode.SetAttribute("val", sVal);
                    configs[0].AppendChild(newNode);

                    node = configs[0].SelectSingleNode(sNode);
                    bModify = true;
                    return true;
                }
                //    return false;

                if (node.Attributes["val"] != null)
                {
                    if (node.Attributes["val"].Value != sVal)
                    {
                        node.Attributes["val"].Value = sVal;
                        bModify = true;
                    }
                }
                return true;
            }

            public bool SetVal(List<Dictionary<string, string>> attrList)
            {
                if (xmldoc == null)
                    return false;

                XmlNodeList items = xmldoc.GetElementsByTagName("items");
                if (items.Count == 0)
                    return false;

                items[0].RemoveAll();
                foreach (Dictionary<string, string> item in attrList)
                {
                    XmlElement nodeItem = xmldoc.CreateElement("item");
                    foreach (KeyValuePair<string, string> kvp in item)
                    {
                        nodeItem.SetAttribute(kvp.Key, kvp.Value);
                    }
                    items[0].AppendChild(nodeItem);
                }
                bModify = true;

                return true;
            }

            public List<Dictionary<string, string>> GetAttr()
            {
                List<Dictionary<string, string>> retList = new List<Dictionary<string, string>>();

                if (xmldoc == null)
                    return retList;

                XmlNodeList items = xmldoc.GetElementsByTagName("items");
                if (items.Count == 0)
                    return retList;

                XmlNodeList itemList = items[0].SelectNodes("item");
                if (itemList.Count == 0)
                    return retList;

                for (int i = 0; i < itemList.Count; i++)
                {
                    retList.Add(new Dictionary<string, string>());
                    for (int j = 0; j < itemList[i].Attributes.Count; j++)
                    {
                        retList.Last().Add(itemList[i].Attributes.Item(j).Name, itemList[i].Attributes.Item(j).Value);
                        //retList.Last().Add(new KeyValuePair<string, string>(itemList[i].Attributes.Item(j).Name, itemList[i].Attributes.Item(j).Value));
                    }
                }
                return retList;
            }

            public bool GetBool(string sNode)
            {
                string sVal = GetVal(sNode);
                if (string.IsNullOrEmpty(sVal))
                {
                    return false;
                }
                return bool.Parse(sVal);
            }

            public float GetFloat(string sNode)
            {
                string sVal = GetVal(sNode);
                if (string.IsNullOrEmpty(sVal))
                {
                    return 0f;
                }
                return float.Parse(sVal);
            }

            public int GetInt(string sNode)
            {
                string sVal = GetVal(sNode);
                if (string.IsNullOrEmpty(sVal))
                {
                    return 0;
                }
                return int.Parse(sVal);
            }

            public string GetString(string sNode)
            {
                return GetVal(sNode);
            }

            private string GetVal(string sNode)
            {
                if (xmldoc == null)
                    return string.Empty;

                XmlNode node = xmldoc.GetElementsByTagName("config")[0].SelectSingleNode(sNode);
                if (node == null)
                    return string.Empty;

                if (node.Attributes["val"] != null)
                {
                    return node.Attributes["val"].Value;
                }
                return string.Empty;
            }

            public void LoadXML()
            {
                Init();
            }

            public void SaveXML()
            {
                if (bModify)
                {
                    xmldoc.Save(sXmlFileName);
                    bModify = false;
                }
                return;
            }
        }

        private static class Utl
        {
            internal static string DrawTextFieldF(Rect rect, string sField, float fMin, float fMax, GUIStyle style)
            {
                string sTmp;
                sTmp = GUI.TextField(rect, sField, style);
                if (sTmp != sField)
                {
                    float fTmp;
                    if (float.TryParse(sTmp, out fTmp))
                    {
                        return Mathf.Clamp(fTmp, fMin, fMax).ToString();
                    }
                }
                return sField;
            }

            internal static int GetPix(int i)
            {
                float f = 1f + (Screen.width / 1280f - 1f) * 0.6f;
                return (int)(f * i);
            }

            internal static bool IsMouseOnRect(Rect rect)
            {
                if (rect.Contains(new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y)))
                {
                    return true;
                }
                return false;
            }

            internal static bool GetAnyMouseDown()
            {
                return Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2);
            }

            internal static bool GetMouseWheelUse()
            {
                return Input.GetAxis("Mouse ScrollWheel") != 0f;
            }
        }
    }
}
