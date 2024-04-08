using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Audio;

namespace TVEngine.Audio
{

    public class SimpleBgmPlayer : MonoBehaviour
    {
        [Serializable]
        public enum BgmAudioSourceId
        {
            One,
            Two,
        }

        [SerializeField]
        private BgmDictionary _bgmDictionary;

        [SerializeField]
        private AudioSource[] _audioSources = new AudioSource[2];

        [SerializeField]
        private float _crossfadeTime = 2.5f;

        private BgmAudioSourceId _currentMainAudioSource = BgmAudioSourceId.One;
        private Coroutine _crossfadeRoutine = null;

        public void PlayBgm(BgmDictionary.BgmIds pBgmId, bool pLoop)
        {
            if (_bgmDictionary.IsBgmUniqueIDValid(pBgmId))
            {
                int sourceId = (int)_currentMainAudioSource;
                _audioSources[sourceId].Stop();
                _audioSources[sourceId].clip = _bgmDictionary.GetBgmAudioClipWithUniqueID(pBgmId);
                _audioSources[sourceId].loop = pLoop;
                _audioSources[sourceId].Play();
            }
        }

        private void ResetAudioSource(BgmAudioSourceId bgmAudioSourceId)
        {
            int sourceId = (int)bgmAudioSourceId;
            _audioSources[sourceId].Stop();
            _audioSources[sourceId].clip = null;
            _audioSources[sourceId].loop = false;
            _audioSources[sourceId].volume = 1.0f;
            _audioSources[sourceId].enabled = false;
        }

        private void ResetAudioSource(AudioSource pAudioSource)
        {
            pAudioSource.Stop();
            pAudioSource.clip = null;
            pAudioSource.loop = false;
            pAudioSource.volume = 1.0f;
            pAudioSource.enabled = false;
        }

        public void CrossFadeBgm(BgmDictionary.BgmIds pBgmId, bool pLoop)
        {
            if (_bgmDictionary.IsBgmUniqueIDValid(pBgmId))
            {
                //If there is already a cross fade in progress just hard swap as it would be weird to match up the crossfades at this point
                if (_crossfadeRoutine != null)
                {
                    StopCoroutine(_crossfadeRoutine);
                    _crossfadeRoutine = null;
                    ResetAudioSource(BgmAudioSourceId.One);
                    ResetAudioSource(BgmAudioSourceId.Two);
                    _currentMainAudioSource = BgmAudioSourceId.One;
                    _audioSources[(int)_currentMainAudioSource].enabled = true;
                    PlayBgm(pBgmId, pLoop);
                }
                else
                {
                    _crossfadeRoutine = StartCoroutine(CrossFadeRoutine(pBgmId, pLoop, pFadeToEmpty: false));
                }
            }
        }

        public void PlayCrossFadeBgmNoAudio()
        {
            if (_crossfadeRoutine != null)
            {
                StopCoroutine(_crossfadeRoutine);
                _crossfadeRoutine = null;
                ResetAudioSource(BgmAudioSourceId.One);
                ResetAudioSource(BgmAudioSourceId.Two);
                _currentMainAudioSource = BgmAudioSourceId.One;
                _audioSources[(int)_currentMainAudioSource].enabled = true;
            }
            else
            {
                _crossfadeRoutine = StartCoroutine(CrossFadeRoutine(BgmDictionary.BgmIds.Count, pLoop: false, pFadeToEmpty: true));
            }
        }
        private IEnumerator CrossFadeRoutine(BgmDictionary.BgmIds pBgmId, bool pLoop, bool pFadeToEmpty)
        {
            float elapsedTime = 0.0f;

            AudioSource fadeOutSource = _audioSources[0];
            AudioSource fadeInSource = _audioSources[1];

            if (_currentMainAudioSource == BgmAudioSourceId.Two)
            {
                fadeOutSource = _audioSources[1];
                fadeInSource = _audioSources[0];
            }

            if (pFadeToEmpty)
            {
                fadeInSource.clip = null;
                fadeInSource.loop = false;
                fadeInSource.volume = 0.0f;
                fadeInSource.enabled = true;
            }
            else
            {
                fadeInSource.clip = _bgmDictionary.GetBgmAudioClipWithUniqueID(pBgmId);
                fadeInSource.loop = pLoop;
                fadeInSource.volume = 0.0f;
                fadeInSource.enabled = true;
                fadeInSource.Play();
            }


            while (elapsedTime < _crossfadeTime)
            {
                elapsedTime += Time.deltaTime;
                fadeOutSource.volume = Mathf.Lerp(1.0f, 0.0f, elapsedTime / _crossfadeTime);
                fadeInSource.volume = 1.0f - fadeOutSource.volume;
                yield return null;
            }

            fadeOutSource.volume = 0.0f;
            fadeInSource.volume = 1.0f;

            ResetAudioSource(fadeOutSource);

            if (_currentMainAudioSource == BgmAudioSourceId.One)
            {
                _currentMainAudioSource = BgmAudioSourceId.Two;
            }
            else
            {
                _currentMainAudioSource = BgmAudioSourceId.One;
            }

            _crossfadeRoutine = null;
        }

        public void SetBgmMixer(BgmAudioSourceId pBgmAudioSourceId, AudioMixerGroup pAudioMixerGroup)
        {
            _audioSources[(int)pBgmAudioSourceId].outputAudioMixerGroup = pAudioMixerGroup;
        }
    }
}
