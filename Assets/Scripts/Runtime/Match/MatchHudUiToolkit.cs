using System;
using System.Collections.Generic;
using Risiko3D.Runtime.Configuration;
using UnityEngine;
using UnityEngine.UIElements;

namespace Risiko3D.Runtime.Match
{
    public sealed class MatchHudUiToolkit : MonoBehaviour
    {
        private const string PanelSettingsResourcePath = "UI/BoardLegend/BoardLegendPanelSettings";

        private HostAuthoritativeMatchLoop _loop;
        private UIDocument _document;

        private VisualElement _root;
        private Label _phaseValue;
        private Label _playerValue;
        private Label _poolValue;
        private Label _poolLabel;
        private Label _selectedValue;
        private Label _statusValue;
        private VisualElement _playerSwatch;
        private Button _primaryButton;
        private Button _advanceButton;
        private Label _hintValue;
        private VisualElement _amountRow;
        private Label _amountLabel;
        private Button _amountDecreaseButton;
        private Button _amountIncreaseButton;
        private Label _amountValue;
        private Label _objectiveTitleValue;
        private Label _objectiveValue;
        private Image _objectiveCardImage;
        private Label _handSummaryValue;
        private ScrollView _cardList;
        private Button _tradeButton;
        private int _lastRenderedPlayerIndex = -1;
        private int _lastRenderedCardCount = -1;
        private string _lastRenderedObjectiveId = string.Empty;
        private readonly Dictionary<string, Sprite> _territoryCardSprites = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Sprite> _objectiveCardSprites = new(StringComparer.Ordinal);
        private Sprite _jokerCardSprite;

        public void Initialize(GameRuntimeConfig _)
        {
            // Reserved for config-driven toggles later.
        }

        private void Start()
        {
            _loop = FindFirstObjectByType<HostAuthoritativeMatchLoop>();
            EnsureDocument();
            BuildSpriteCaches();
            BuildUi();
            Refresh();
        }

        private void Update()
        {
            if (_loop == null)
            {
                _loop = FindFirstObjectByType<HostAuthoritativeMatchLoop>();
            }

            Refresh();
        }

        private void EnsureDocument()
        {
            _document = GetComponent<UIDocument>();
            if (_document == null)
            {
                _document = gameObject.AddComponent<UIDocument>();
            }

            if (_document.panelSettings == null)
            {
                var settings = Resources.Load<PanelSettings>(PanelSettingsResourcePath);
                if (settings != null)
                {
                    _document.panelSettings = settings;
                }
                else
                {
                    var runtimeSettings = ScriptableObject.CreateInstance<PanelSettings>();
                    runtimeSettings.clearColor = false;
                    runtimeSettings.sortingOrder = 650;
                    runtimeSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
                    runtimeSettings.referenceResolution = new Vector2Int(1920, 1080);
                    _document.panelSettings = runtimeSettings;
                }
            }
        }

        private void BuildUi()
        {
            _root = _document.rootVisualElement;
            if (_root == null)
            {
                return;
            }

            _root.Clear();
            _root.style.flexGrow = 1f;
            _root.style.paddingLeft = 0f;
            _root.style.paddingTop = 0f;

            var panel = new VisualElement();
            panel.name = "match-hud-panel";
            panel.style.position = Position.Absolute;
            panel.style.right = 16f;
            panel.style.bottom = 16f;
            panel.style.width = 360f;
            panel.style.backgroundColor = new Color(0.055f, 0.070f, 0.110f, 0.94f);
            panel.style.borderTopLeftRadius = 12f;
            panel.style.borderTopRightRadius = 12f;
            panel.style.borderBottomLeftRadius = 12f;
            panel.style.borderBottomRightRadius = 12f;
            panel.style.borderTopWidth = 1f;
            panel.style.borderRightWidth = 1f;
            panel.style.borderBottomWidth = 1f;
            panel.style.borderLeftWidth = 1f;
            panel.style.borderTopColor = new Color(1f, 1f, 1f, 0.12f);
            panel.style.borderRightColor = new Color(1f, 1f, 1f, 0.12f);
            panel.style.borderBottomColor = new Color(1f, 1f, 1f, 0.12f);
            panel.style.borderLeftColor = new Color(1f, 1f, 1f, 0.12f);
            panel.style.paddingLeft = 12f;
            panel.style.paddingRight = 12f;
            panel.style.paddingTop = 10f;
            panel.style.paddingBottom = 12f;
            _root.Add(panel);

            var title = new Label("Match Controls");
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.fontSize = 15f;
            title.style.color = new Color(1f, 0.95f, 0.82f, 1f);
            title.style.marginBottom = 8f;
            panel.Add(title);

            panel.Add(CreateInfoRow("Phase", out _phaseValue));
            panel.Add(CreatePlayerRow());
            panel.Add(CreateInfoRow("Pool", out _poolValue, out _poolLabel));
            panel.Add(CreateInfoRow("Selected", out _selectedValue));

            _statusValue = new Label("Ready");
            _statusValue.style.color = new Color(0.88f, 0.90f, 0.95f, 0.95f);
            _statusValue.style.fontSize = 12f;
            _statusValue.style.whiteSpace = WhiteSpace.Normal;
            _statusValue.style.minHeight = 34f;
            _statusValue.style.marginTop = 8f;
            panel.Add(_statusValue);

            _hintValue = new Label(string.Empty);
            _hintValue.style.color = new Color(1f, 0.90f, 0.65f, 0.95f);
            _hintValue.style.fontSize = 11f;
            _hintValue.style.whiteSpace = WhiteSpace.Normal;
            _hintValue.style.minHeight = 26f;
            _hintValue.style.marginTop = 6f;
            panel.Add(_hintValue);

            _amountRow = new VisualElement();
            _amountRow.style.flexDirection = FlexDirection.Row;
            _amountRow.style.alignItems = Align.Center;
            _amountRow.style.marginTop = 2f;
            panel.Add(_amountRow);

            _amountLabel = new Label("Amount");
            _amountLabel.style.fontSize = 10f;
            _amountLabel.style.color = new Color(0.86f, 0.90f, 0.98f, 0.92f);
            _amountLabel.style.minWidth = 98f;
            _amountRow.Add(_amountLabel);

            _amountDecreaseButton = new Button(OnAmountDecreaseClicked);
            _amountDecreaseButton.text = "-";
            _amountDecreaseButton.style.width = 24f;
            _amountDecreaseButton.style.height = 22f;
            _amountDecreaseButton.style.unityFontStyleAndWeight = FontStyle.Bold;
            _amountRow.Add(_amountDecreaseButton);

            _amountValue = new Label("1");
            _amountValue.style.width = 34f;
            _amountValue.style.unityTextAlign = TextAnchor.MiddleCenter;
            _amountValue.style.color = new Color(0.95f, 0.96f, 0.99f, 0.98f);
            _amountValue.style.fontSize = 11f;
            _amountValue.style.unityFontStyleAndWeight = FontStyle.Bold;
            _amountRow.Add(_amountValue);

            _amountIncreaseButton = new Button(OnAmountIncreaseClicked);
            _amountIncreaseButton.text = "+";
            _amountIncreaseButton.style.width = 24f;
            _amountIncreaseButton.style.height = 22f;
            _amountIncreaseButton.style.unityFontStyleAndWeight = FontStyle.Bold;
            _amountRow.Add(_amountIncreaseButton);
            SetAmountControlsVisible(false);

            var dockTitle = new Label("Card Dock");
            dockTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            dockTitle.style.fontSize = 12f;
            dockTitle.style.color = new Color(0.95f, 0.92f, 0.78f, 0.98f);
            dockTitle.style.marginTop = 6f;
            panel.Add(dockTitle);

            var objectiveCard = new VisualElement();
            objectiveCard.style.marginTop = 4f;
            objectiveCard.style.paddingTop = 6f;
            objectiveCard.style.paddingBottom = 6f;
            objectiveCard.style.paddingLeft = 8f;
            objectiveCard.style.paddingRight = 8f;
            objectiveCard.style.backgroundColor = new Color(0.19f, 0.15f, 0.07f, 0.92f);
            objectiveCard.style.borderTopLeftRadius = 8f;
            objectiveCard.style.borderTopRightRadius = 8f;
            objectiveCard.style.borderBottomLeftRadius = 8f;
            objectiveCard.style.borderBottomRightRadius = 8f;
            objectiveCard.style.borderTopWidth = 1f;
            objectiveCard.style.borderRightWidth = 1f;
            objectiveCard.style.borderBottomWidth = 1f;
            objectiveCard.style.borderLeftWidth = 1f;
            objectiveCard.style.borderTopColor = new Color(1f, 0.84f, 0.55f, 0.35f);
            objectiveCard.style.borderRightColor = new Color(1f, 0.84f, 0.55f, 0.35f);
            objectiveCard.style.borderBottomColor = new Color(1f, 0.84f, 0.55f, 0.35f);
            objectiveCard.style.borderLeftColor = new Color(1f, 0.84f, 0.55f, 0.35f);
            panel.Add(objectiveCard);

            _objectiveCardImage = new Image();
            _objectiveCardImage.scaleMode = ScaleMode.ScaleToFit;
            _objectiveCardImage.style.height = 100f;
            _objectiveCardImage.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;
            _objectiveCardImage.style.display = DisplayStyle.None;
            objectiveCard.Add(_objectiveCardImage);

            _objectiveTitleValue = new Label("Obiettivo");
            _objectiveTitleValue.style.color = new Color(1f, 0.92f, 0.72f, 0.98f);
            _objectiveTitleValue.style.fontSize = 12f;
            _objectiveTitleValue.style.unityFontStyleAndWeight = FontStyle.Bold;
            objectiveCard.Add(_objectiveTitleValue);

            _objectiveValue = new Label("-");
            _objectiveValue.style.color = new Color(0.94f, 0.95f, 0.98f, 0.95f);
            _objectiveValue.style.fontSize = 10f;
            _objectiveValue.style.whiteSpace = WhiteSpace.Normal;
            _objectiveValue.style.marginTop = 2f;
            _objectiveValue.style.minHeight = 28f;
            objectiveCard.Add(_objectiveValue);

            _handSummaryValue = new Label("Hand: 0 cards");
            _handSummaryValue.style.color = new Color(0.84f, 0.89f, 0.98f, 0.92f);
            _handSummaryValue.style.fontSize = 11f;
            _handSummaryValue.style.marginTop = 2f;
            panel.Add(_handSummaryValue);

            _tradeButton = new Button(OnTradeActionClicked);
            _tradeButton.style.marginTop = 4f;
            _tradeButton.style.height = 28f;
            _tradeButton.style.unityFontStyleAndWeight = FontStyle.Bold;
            _tradeButton.style.backgroundColor = new Color(0.52f, 0.40f, 0.18f, 0.95f);
            _tradeButton.style.color = new Color(0.98f, 0.95f, 0.88f, 1f);
            _tradeButton.text = "Trade-In";
            panel.Add(_tradeButton);

            _cardList = new ScrollView(ScrollViewMode.Horizontal);
            _cardList.style.height = 122f;
            _cardList.style.backgroundColor = new Color(0.03f, 0.04f, 0.07f, 0.62f);
            _cardList.style.borderTopWidth = 1f;
            _cardList.style.borderRightWidth = 1f;
            _cardList.style.borderBottomWidth = 1f;
            _cardList.style.borderLeftWidth = 1f;
            _cardList.style.borderTopColor = new Color(1f, 1f, 1f, 0.08f);
            _cardList.style.borderRightColor = new Color(1f, 1f, 1f, 0.08f);
            _cardList.style.borderBottomColor = new Color(1f, 1f, 1f, 0.08f);
            _cardList.style.borderLeftColor = new Color(1f, 1f, 1f, 0.08f);
            _cardList.style.marginTop = 4f;
            _cardList.contentContainer.style.flexDirection = FlexDirection.Row;
            _cardList.contentContainer.style.alignItems = Align.Stretch;
            _cardList.contentContainer.style.paddingLeft = 4f;
            _cardList.contentContainer.style.paddingRight = 4f;
            panel.Add(_cardList);

            var buttons = new VisualElement();
            buttons.style.flexDirection = FlexDirection.Row;
            buttons.style.marginTop = 4f;
            panel.Add(buttons);

            _primaryButton = new Button(OnPrimaryActionClicked);
            _primaryButton.style.flexGrow = 1f;
            _primaryButton.style.height = 34f;
            _primaryButton.style.unityFontStyleAndWeight = FontStyle.Bold;
            _primaryButton.style.backgroundColor = new Color(0.22f, 0.56f, 0.30f, 0.95f);
            _primaryButton.style.color = Color.white;
            buttons.Add(_primaryButton);

            _advanceButton = new Button(OnAdvanceActionClicked);
            _advanceButton.style.width = 126f;
            _advanceButton.style.height = 34f;
            _advanceButton.style.unityFontStyleAndWeight = FontStyle.Bold;
            _advanceButton.style.backgroundColor = new Color(0.32f, 0.33f, 0.40f, 0.95f);
            _advanceButton.style.color = new Color(0.96f, 0.96f, 0.98f, 1f);
            _advanceButton.style.marginLeft = 8f;
            buttons.Add(_advanceButton);
        }

        private VisualElement CreateInfoRow(string labelText, out Label valueLabel)
        {
            Label ignored;
            return CreateInfoRow(labelText, out valueLabel, out ignored);
        }

        private VisualElement CreateInfoRow(string labelText, out Label valueLabel, out Label labelRef)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.justifyContent = Justify.SpaceBetween;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 6f;

            var label = new Label(labelText);
            label.style.color = new Color(0.78f, 0.82f, 0.90f, 0.96f);
            label.style.fontSize = 12f;
            row.Add(label);
            labelRef = label;

            valueLabel = new Label("-");
            valueLabel.style.color = new Color(0.96f, 0.97f, 0.99f, 0.98f);
            valueLabel.style.fontSize = 13f;
            valueLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            row.Add(valueLabel);
            return row;
        }

        private VisualElement CreatePlayerRow()
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.justifyContent = Justify.SpaceBetween;
            row.style.alignItems = Align.Center;

            var left = new Label("Active Player");
            left.style.color = new Color(0.78f, 0.82f, 0.90f, 0.96f);
            left.style.fontSize = 12f;
            row.Add(left);

            var right = new VisualElement();
            right.style.flexDirection = FlexDirection.Row;
            right.style.alignItems = Align.Center;
            row.Add(right);

            _playerSwatch = new VisualElement();
            _playerSwatch.style.width = 12f;
            _playerSwatch.style.height = 12f;
            _playerSwatch.style.borderTopLeftRadius = 3f;
            _playerSwatch.style.borderTopRightRadius = 3f;
            _playerSwatch.style.borderBottomLeftRadius = 3f;
            _playerSwatch.style.borderBottomRightRadius = 3f;
            _playerSwatch.style.backgroundColor = Color.white;
            _playerSwatch.style.marginRight = 6f;
            right.Add(_playerSwatch);

            _playerValue = new Label("-");
            _playerValue.style.color = new Color(0.96f, 0.97f, 0.99f, 0.98f);
            _playerValue.style.fontSize = 13f;
            _playerValue.style.unityFontStyleAndWeight = FontStyle.Bold;
            right.Add(_playerValue);

            return row;
        }

        private void Refresh()
        {
            if (_loop == null || _root == null)
            {
                return;
            }

            _phaseValue.text = _loop.PhaseName;
            _playerValue.text = _loop.ActivePlayerId;
            _playerSwatch.style.backgroundColor = _loop.ActivePlayerColor;
            _objectiveTitleValue.text = _loop.ActivePlayerObjectiveCardTitle;
            _objectiveValue.text = _loop.ActivePlayerObjectiveCard;
            if (_lastRenderedObjectiveId != _loop.ActivePlayerObjectiveCardBaseId)
            {
                ApplyObjectiveCardSprite(_loop.ActivePlayerObjectiveCardBaseId);
                _lastRenderedObjectiveId = _loop.ActivePlayerObjectiveCardBaseId;
            }
            _handSummaryValue.text = $"Hand: {_loop.ActivePlayerHandCount} cards";

            var selected = _loop.SelectedTerritoryName;
            _selectedValue.text = string.IsNullOrWhiteSpace(selected) ? "none" : selected;
            _statusValue.text = _loop.StatusMessage;

            var phase = _loop.PhaseName;
            var hasSelection = !string.IsNullOrWhiteSpace(_loop.SelectedTerritoryId);
            var hasSource = !string.IsNullOrWhiteSpace(_loop.PendingSourceTerritoryId);

            if (phase == "SetupClaim")
            {
                _poolLabel.text = "Unclaimed";
                _poolValue.text = _loop.UnclaimedTerritoryCount.ToString();
                _primaryButton.text = "Claim Territory";
                _advanceButton.text = "Setup Locked";
                _advanceButton.SetEnabled(false);
                _hintValue.text = "Players take turns claiming one unoccupied territory.";
                _tradeButton.SetEnabled(false);
            }
            else if (phase == "SetupDeploy")
            {
                _poolLabel.text = "Setup Armies";
                _poolValue.text = _loop.ActiveSetupArmiesRemaining.ToString();
                _primaryButton.text = "Place +1 Army";
                _advanceButton.text = "Setup Locked";
                _advanceButton.SetEnabled(false);
                _hintValue.text = "Setup turn: place up to 3 armies on owned territories.";
                _tradeButton.SetEnabled(false);
            }
            else if (phase == "Reinforce")
            {
                _poolLabel.text = "Reinforcements";
                _poolValue.text = _loop.ActiveReinforcementPool.ToString();
                _primaryButton.text = "Add +1 Army";
                _advanceButton.text = "To Attack";
                _advanceButton.SetEnabled(true);
                _hintValue.text = hasSelection
                    ? "Select your territory and add armies until pool reaches zero."
                    : "Select one of your territories, then click Add +1 Army.";
                _tradeButton.text = _loop.CanActivePlayerTradeCards
                    ? $"Trade-In (+{_loop.BestActivePlayerTradeInValue})"
                    : "Trade-In (none)";
                _tradeButton.SetEnabled(_loop.CanActivePlayerTradeCards);
                SetAmountControlsVisible(false);
            }
            else if (phase == "Attack")
            {
                _poolLabel.text = "Reinforcements";
                _poolValue.text = _loop.ActiveReinforcementPool.ToString();
                _primaryButton.text = _loop.HasPendingCaptureMove
                    ? "Confirm Capture Move"
                    : hasSource ? "Attack Target" : "Set Attack Source";
                _advanceButton.text = "To Fortify";
                _advanceButton.SetEnabled(true);
                _hintValue.text = _loop.HasPendingCaptureMove
                    ? $"Resolve capture move first. Min {_loop.PendingCaptureMinArmies}, Max {_loop.PendingCaptureMaxArmies}."
                    : hasSource
                        ? "Now select adjacent enemy territory, then click Attack Target."
                        : "Select owned territory with 2+ armies, then Set Attack Source.";
                _tradeButton.SetEnabled(false);

                if (_loop.HasPendingCaptureMove)
                {
                    SetAmountControlsVisible(true);
                    _amountLabel.text = "Capture Move";
                    _amountValue.text = _loop.PendingCaptureCurrentArmies.ToString();
                    _amountDecreaseButton.SetEnabled(_loop.PendingCaptureCurrentArmies > _loop.PendingCaptureMinArmies);
                    _amountIncreaseButton.SetEnabled(_loop.PendingCaptureCurrentArmies < _loop.PendingCaptureMaxArmies);
                }
                else if (hasSource)
                {
                    SetAmountControlsVisible(true);
                    _amountLabel.text = "Attack Dice";
                    _amountValue.text = _loop.PendingAttackDice.ToString();
                    _amountDecreaseButton.SetEnabled(_loop.PendingAttackDice > 1);
                    _amountIncreaseButton.SetEnabled(_loop.PendingAttackDice < _loop.PendingAttackMaxDice);
                }
                else
                {
                    SetAmountControlsVisible(false);
                }
            }
            else
            {
                _poolLabel.text = "Reinforcements";
                _poolValue.text = _loop.ActiveReinforcementPool.ToString();
                _primaryButton.text = hasSource ? "Fortify +1" : "Set Fortify Source";
                _advanceButton.text = "End Turn";
                _advanceButton.SetEnabled(true);
                _hintValue.text = hasSource
                    ? "Select adjacent owned territory, then Fortify +1 (turn ends)."
                    : "Fortify is optional. Set source or click End Turn.";
                _tradeButton.SetEnabled(false);

                if (hasSource)
                {
                    SetAmountControlsVisible(true);
                    _amountLabel.text = "Fortify Armies";
                    _amountValue.text = _loop.PendingFortifyArmies.ToString();
                    _amountDecreaseButton.SetEnabled(_loop.PendingFortifyArmies > 1);
                    _amountIncreaseButton.SetEnabled(_loop.PendingFortifyArmies < _loop.PendingFortifyMaxArmies);
                }
                else
                {
                    SetAmountControlsVisible(false);
                }
            }

            var canPrimary = hasSelection || (phase == "Attack" && _loop.HasPendingCaptureMove);
            _primaryButton.SetEnabled(canPrimary);

            var currentCardCount = _loop.ActivePlayerHandCount;
            if (_lastRenderedPlayerIndex != _loop.ActivePlayerIndex || _lastRenderedCardCount != currentCardCount)
            {
                RebuildCardDock();
            }
        }

        private void OnPrimaryActionClicked()
        {
            _loop?.UiSubmitAction();
            Refresh();
        }

        private void OnAdvanceActionClicked()
        {
            _loop?.UiAdvanceAction();
            Refresh();
        }

        private void OnTradeActionClicked()
        {
            _loop?.UiTradeCards();
            Refresh();
        }

        private void OnAmountDecreaseClicked()
        {
            if (_loop == null)
            {
                return;
            }

            if (_loop.PhaseName == "Attack")
            {
                if (_loop.HasPendingCaptureMove)
                {
                    _loop.UiCaptureMoveDecrease();
                }
                else
                {
                    _loop.UiAttackDiceDecrease();
                }
            }
            else if (_loop.PhaseName == "Fortify")
            {
                _loop.UiFortifyArmiesDecrease();
            }

            Refresh();
        }

        private void OnAmountIncreaseClicked()
        {
            if (_loop == null)
            {
                return;
            }

            if (_loop.PhaseName == "Attack")
            {
                if (_loop.HasPendingCaptureMove)
                {
                    _loop.UiCaptureMoveIncrease();
                }
                else
                {
                    _loop.UiAttackDiceIncrease();
                }
            }
            else if (_loop.PhaseName == "Fortify")
            {
                _loop.UiFortifyArmiesIncrease();
            }

            Refresh();
        }

        private void SetAmountControlsVisible(bool visible)
        {
            _amountRow.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void RebuildCardDock()
        {
            _cardList.Clear();
            if (_loop == null)
            {
                return;
            }

            var cardsUi = _loop.GetActivePlayerHandCardsUi();
            if (cardsUi == null || cardsUi.Count == 0)
            {
                var empty = new Label("No cards in hand.");
                empty.style.fontSize = 10f;
                empty.style.color = new Color(0.72f, 0.77f, 0.86f, 0.78f);
                _cardList.Add(empty);
                _lastRenderedPlayerIndex = _loop.ActivePlayerIndex;
                _lastRenderedCardCount = 0;
                return;
            }

            for (var i = 0; i < cardsUi.Count; i++)
            {
                _cardList.Add(CreateCardVisual(cardsUi[i], ResolveCardSprite(cardsUi[i])));
            }

            _lastRenderedPlayerIndex = _loop.ActivePlayerIndex;
            _lastRenderedCardCount = cardsUi.Count;
        }

        private static VisualElement CreateCardVisual(HostAuthoritativeMatchLoop.HandCardUiData card, Sprite sprite)
        {
            var cardRoot = new VisualElement();
            cardRoot.style.width = 112f;
            cardRoot.style.height = 108f;
            cardRoot.style.marginTop = 5f;
            cardRoot.style.marginBottom = 5f;
            cardRoot.style.marginRight = 6f;
            cardRoot.style.paddingTop = 5f;
            cardRoot.style.paddingBottom = 5f;
            cardRoot.style.paddingLeft = 6f;
            cardRoot.style.paddingRight = 6f;
            cardRoot.style.borderTopLeftRadius = 8f;
            cardRoot.style.borderTopRightRadius = 8f;
            cardRoot.style.borderBottomLeftRadius = 8f;
            cardRoot.style.borderBottomRightRadius = 8f;
            cardRoot.style.borderTopWidth = 1f;
            cardRoot.style.borderRightWidth = 1f;
            cardRoot.style.borderBottomWidth = 1f;
            cardRoot.style.borderLeftWidth = 1f;
            cardRoot.style.borderTopColor = new Color(1f, 1f, 1f, 0.18f);
            cardRoot.style.borderRightColor = new Color(1f, 1f, 1f, 0.18f);
            cardRoot.style.borderBottomColor = new Color(1f, 1f, 1f, 0.18f);
            cardRoot.style.borderLeftColor = new Color(1f, 1f, 1f, 0.18f);

            var baseColor = card.Symbol switch
            {
                "infantry" => new Color(0.21f, 0.39f, 0.78f, 0.95f),
                "cavalry" => new Color(0.27f, 0.57f, 0.31f, 0.95f),
                "artillery" => new Color(0.63f, 0.32f, 0.20f, 0.95f),
                "joker" => new Color(0.48f, 0.30f, 0.66f, 0.95f),
                _ => new Color(0.29f, 0.33f, 0.43f, 0.95f)
            };
            cardRoot.style.backgroundColor = new Color(baseColor.r * 0.35f, baseColor.g * 0.35f, baseColor.b * 0.35f, 0.92f);

            var ribbon = new VisualElement();
            ribbon.style.height = 14f;
            ribbon.style.marginBottom = 4f;
            ribbon.style.backgroundColor = baseColor;
            ribbon.style.borderTopLeftRadius = 5f;
            ribbon.style.borderTopRightRadius = 5f;
            ribbon.style.borderBottomLeftRadius = 5f;
            ribbon.style.borderBottomRightRadius = 5f;
            cardRoot.Add(ribbon);

            var symbol = new Label(card.IsJoker ? "JOLLY" : card.Symbol.ToUpperInvariant());
            symbol.style.fontSize = 9f;
            symbol.style.unityFontStyleAndWeight = FontStyle.Bold;
            symbol.style.color = new Color(1f, 0.98f, 0.92f, 0.96f);
            symbol.style.unityTextAlign = TextAnchor.MiddleCenter;
            ribbon.Add(symbol);

            if (sprite != null)
            {
                var image = new Image();
                image.sprite = sprite;
                image.scaleMode = ScaleMode.ScaleToFit;
                image.style.height = 56f;
                image.style.marginBottom = 4f;
                cardRoot.Add(image);
            }

            var title = new Label(card.DisplayName);
            title.style.fontSize = 10f;
            title.style.color = new Color(0.95f, 0.97f, 1f, 0.98f);
            title.style.whiteSpace = WhiteSpace.Normal;
            title.style.flexGrow = 1f;
            cardRoot.Add(title);

            return cardRoot;
        }

        private void BuildSpriteCaches()
        {
            _territoryCardSprites.Clear();
            _objectiveCardSprites.Clear();
            _jokerCardSprite = null;

            var territorySprites = Resources.LoadAll<Sprite>("Cards/Territory");
            for (var i = 0; i < territorySprites.Length; i++)
            {
                var sprite = territorySprites[i];
                if (sprite == null)
                {
                    continue;
                }

                var key = NormalizeCardId(sprite.name);
                if (!_territoryCardSprites.ContainsKey(key))
                {
                    _territoryCardSprites[key] = sprite;
                }
            }

            var objectiveSprites = Resources.LoadAll<Sprite>("Cards/Objective");
            for (var i = 0; i < objectiveSprites.Length; i++)
            {
                var sprite = objectiveSprites[i];
                if (sprite == null)
                {
                    continue;
                }

                var key = NormalizeCardId(sprite.name).Replace("_", "-");
                if (!_objectiveCardSprites.ContainsKey(key))
                {
                    _objectiveCardSprites[key] = sprite;
                }
            }

            var jokerSprites = Resources.LoadAll<Sprite>("Cards/Joker");
            if (jokerSprites.Length > 0)
            {
                _jokerCardSprite = jokerSprites[0];
            }
        }

        private void ApplyObjectiveCardSprite(string objectiveBaseId)
        {
            if (string.IsNullOrWhiteSpace(objectiveBaseId))
            {
                _objectiveCardImage.style.display = DisplayStyle.None;
                return;
            }

            var key = NormalizeCardId(objectiveBaseId).Replace("_", "-");
            if (_objectiveCardSprites.TryGetValue(key, out var sprite) && sprite != null)
            {
                _objectiveCardImage.sprite = sprite;
                _objectiveCardImage.style.display = DisplayStyle.Flex;
                return;
            }

            _objectiveCardImage.style.display = DisplayStyle.None;
        }

        private Sprite ResolveCardSprite(HostAuthoritativeMatchLoop.HandCardUiData card)
        {
            if (card.IsJoker)
            {
                return _jokerCardSprite;
            }

            var key = NormalizeCardId(card.CardId);
            return _territoryCardSprites.TryGetValue(key, out var sprite) ? sprite : null;
        }

        private static string NormalizeCardId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            return value.Trim().Replace(" ", "_").Replace("-", "_").ToLowerInvariant();
        }
    }
}
