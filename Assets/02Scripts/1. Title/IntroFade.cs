using alpha.scene;
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace alpha.title.intro
{
    [Serializable]
    public struct FadeData
    {
        public Image IntroImg;
        public float FadeInTime;
        public float StayTime;     // 유지 시간
        public float FadeOutTime;
    }

    [Serializable]
    public struct IntroSequenceData
    {
        public FadeData[] FadeData;
        public int FadeCount => FadeData?.Length ?? 0;
        public AudioClip AudioClip;
    }

    public class IntroFade : MonoBehaviour
    {
        [SerializeField]
        private float _startDelay;
        [SerializeField]
        private IntroSequenceData[] _introSequences;
        [SerializeField]
        private AudioSource _audioSource;

        private Coroutine _playRoutine;
        private Image _currentImage;
        private int _sequenceIndex;
        private int _fadeIndex;
        private bool _isLeaving;

        private void OnEnable()
        {
            foreach (var seq in _introSequences)
            {
                foreach (var seq2 in seq.FadeData)
                {
                    seq2.IntroImg.gameObject.SetActive(false);
                }
            }
        }

        private void Start()
        {
            _playRoutine = StartCoroutine(PlayAll());
        }

        private IEnumerator PlayAll()
        {
            if(_sequenceIndex == 0) yield return new WaitForSeconds(_startDelay);

            while (_sequenceIndex < _introSequences.Length)
            {
                var seq = _introSequences[_sequenceIndex];

                // 현재 시퀀스가 끝났으면 다음 시퀀스로 이동
                if(_fadeIndex >= seq.FadeCount)
                {
                    _sequenceIndex++;
                    _fadeIndex = 0;
                    continue;
                }

                var data = seq.FadeData[_fadeIndex];
                _currentImage = data.IntroImg;

                yield return FadeIn(data);
                yield return new WaitForSeconds(data.StayTime);
                yield return FadeOut(data);

                _currentImage = null;
                _fadeIndex++;
            }

            Skip();
        }

        private IEnumerator FadeIn(FadeData p_Fade)
        {
            Image image = p_Fade.IntroImg;
            Color color = image.color;
            color.a = 0f;
            image.color = color;
            image.gameObject.SetActive(true);

            if (p_Fade.FadeInTime <= 0f)
            {
                color.a = 1f;
                image.color = color;
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < p_Fade.FadeInTime)
            {
                elapsed += Time.deltaTime;
                color.a = Mathf.Clamp01(elapsed / p_Fade.FadeInTime);
                image.color = color;
                yield return null;
            }

            color.a = 1f;
            image.color = color;
        }

        private IEnumerator FadeOut(FadeData p_Fade)
        {
            Image image = p_Fade.IntroImg;
            Color color = image.color;
            float elapsed = 0f;

            while (elapsed < p_Fade.FadeOutTime)
            {
                elapsed += Time.deltaTime;
                color.a = 1f - Mathf.Clamp01(elapsed / p_Fade.FadeOutTime);
                image.color = color;
                yield return null;
            }

            color.a = 0f;
            image.color = color;
            image.gameObject.SetActive(false);
        }

        // 다음 시퀀스로 전환
        public void Next()
        {
            if (_isLeaving) return;

            StopCurrentFade(); // 현재 페이드 중단 및 이미지 숨김
            _sequenceIndex++;
            _fadeIndex = 0;

            // 마지막 시퀀스일 경우
            if(_sequenceIndex >= _introSequences.Length)
            {
                Skip();
                return;
            }

            _playRoutine = StartCoroutine(PlayAll());
        }

        public void Prev()
        {
            StopCurrentFade();
            _sequenceIndex = Mathf.Max(0, _sequenceIndex - 1);
            _fadeIndex = 0;

            _playRoutine = StartCoroutine(PlayAll());
        }

        public void Skip()
        {
            if (_isLeaving) return;
            _isLeaving = true;

            StopCurrentFade();
            SceneLoader.LoadLobbyScene();
        }

        private void StopCurrentFade()
        {
            if(_playRoutine != null)
            {
                StopCoroutine(_playRoutine);
                _playRoutine = null;
            }

            // 중간에 멈춘 이미지가 화면에 남지 않도록 정리
            if (_currentImage != null)
            {
                Color color = _currentImage.color;
                color.a = 0f;
                _currentImage.color = color;
                _currentImage.gameObject.SetActive(false);
                _currentImage = null;
            }
        }
    }
}