// soundTest.cpp : Этот файл содержит функцию "main". Здесь начинается и заканчивается выполнение программы.
//
#include <fftw3.h>
#include <windows.h>
#include <mmdeviceapi.h>
#include <audioclient.h>
#include <endpointvolume.h>
#include <iostream>
#include <vector>
#include <comdef.h>
#include <cmath>
#include <algorithm>
#include <mfapi.h>
#include <mfidl.h>
#include <mfobjects.h>
#include <mfplay.h>

void ProcessAudioData(std::vector<BYTE>& processedData, BYTE* buffer, UINT32 numFrames, UINT32 numChannels, float gain) {
    // Преобразуем буфер в float* для обработки
    float* floatBuffer = reinterpret_cast<float*>(buffer);

    // Для каждого сэмпла в буфере
    for (UINT32 i = 0; i < numFrames * numChannels; ++i) {
        // Применяем усиление
        float audio = floatBuffer[i] * gain;

        // Копируем результат в processedData
        // Преобразуем результат обратно в байты и добавляем в processedData
        processedData.push_back(reinterpret_cast<BYTE*>(&audio)[0]);
        processedData.push_back(reinterpret_cast<BYTE*>(&audio)[1]);
        processedData.push_back(reinterpret_cast<BYTE*>(&audio)[2]);
        processedData.push_back(reinterpret_cast<BYTE*>(&audio)[3]);
    }
}


IMMDevice* getAudioEndpointDevice()
{
    IMMDevice* pDevice = nullptr;
    HRESULT hr = CoInitialize(nullptr);
    if (FAILED(hr)) {
        std::cerr << "CoInitialize failed!" << std::endl;
        return nullptr;
    }

    // Получаем интерфейс устройства вывода
    IMMDeviceEnumerator* pDeviceEnumerator = nullptr;
    hr = CoCreateInstance(__uuidof(MMDeviceEnumerator), nullptr, CLSCTX_ALL, IID_PPV_ARGS(&pDeviceEnumerator));
    if (FAILED(hr)) {
        std::cerr << "Failed to create device enumerator!" << std::endl;
        CoUninitialize();
        return nullptr;
    }

    // Получаем стандартное устройство вывода (динамики или наушники)
    hr = pDeviceEnumerator->GetDefaultAudioEndpoint(eRender, eConsole, &pDevice);
    if (FAILED(hr)) {
        std::cerr << "Failed to get default audio endpoint!" << std::endl;
        pDeviceEnumerator->Release();
        CoUninitialize();
        return nullptr;
    }
    pDeviceEnumerator->Release();
    return pDevice;
}


int main()
{
    //#pragma region  volume scalar
    //    IMMDevice* pDevice = nullptr;
    //    getAudioEndpointDevice(pDevice);
    //    HRESULT hr = 0;
    //    // Получаем интерфейс IAudioEndpointVolume для изменения громкости
    //    IAudioEndpointVolume* pAudioEndpointVolume = nullptr;
    //    hr = pDevice->Activate(__uuidof(IAudioEndpointVolume), CLSCTX_ALL, nullptr, reinterpret_cast<void**>(&pAudioEndpointVolume));
    //    if (FAILED(hr)) {
    //        std::cerr << "Failed to activate audio endpoint volume interface!" << std::endl;
    //        pDevice->Release();
    //        CoUninitialize();
    //        return -1;
    //    }
    //
    //    // Получаем текущий уровень громкости
    //    float currentVolume = 0.0f;
    //    hr = pAudioEndpointVolume->GetMasterVolumeLevelScalar(&currentVolume);
    //    if (FAILED(hr)) {
    //        std::cerr << "Failed to get current volume!" << std::endl;
    //    }
    //    else {
    //        std::cout << "Current volume: " << currentVolume * 100 << "%" << std::endl;
    //    }
    //
    //    // Устанавливаем новый уровень громкости (например, 50%)
    //    float newVolume = 0.5f;  // от 0.0f (тишина) до 1.0f (максимальная громкость)
    //    hr = pAudioEndpointVolume->SetMasterVolumeLevelScalar(newVolume, nullptr);
    //    if (FAILED(hr)) {
    //        std::cerr << "Failed to set new volume!" << std::endl;
    //    }
    //    else {
    //        std::cout << "Volume set to: " << newVolume * 100 << "%" << std::endl;
    //    }
    //
    //    // Освобождаем ресурсы
    //    pAudioEndpointVolume->Release();
    //    pDevice->Release();
    //
    //    // Завершаем работу COM
    //    CoUninitialize();
    //    return 0;
    //#pragma endregion
    IMMDevice* pDevice = getAudioEndpointDevice();
    #pragma region capture client
    IAudioClient* pAudioClient = nullptr;
    HRESULT hr = pDevice->Activate(__uuidof(IAudioClient), CLSCTX_ALL, nullptr, reinterpret_cast<void**>(&pAudioClient));
    if (FAILED(hr)) {
        std::cerr << "Failed to activate audio client!" << std::endl;
        pDevice->Release();
        CoUninitialize();
        return 0;
    }
    WAVEFORMATEX* pFormat = nullptr;
    hr = pAudioClient->GetMixFormat(&pFormat);
    if (FAILED(hr)) {
        std::cerr << "Failed to get mix format!" << std::endl;
        pAudioClient->Release();
        pDevice->Release();
        CoUninitialize();
        return 0;
    }
    hr = pAudioClient->Initialize(AUDCLNT_SHAREMODE_SHARED, AUDCLNT_STREAMFLAGS_LOOPBACK, 10000000, 0, pFormat, nullptr);
    if (FAILED(hr)) {
        std::cerr << "Failed to initialize audio client!" << std::endl;
        pAudioClient->Release();
        pDevice->Release();
        CoUninitialize();
        return 0;
    }
    IAudioCaptureClient* pCaptureClient = nullptr;
    hr = pAudioClient->GetService(IID_PPV_ARGS(&pCaptureClient));
    if (FAILED(hr)) {
        std::cerr << "Failed to get capture client!" << std::endl;
        pAudioClient->Release();
        pDevice->Release();
        CoUninitialize();
        return 0;
    }
#pragma endregion
#pragma region render client
    IAudioClient* pAudioClientRender = nullptr;
    hr = pDevice->Activate(__uuidof(IAudioClient), CLSCTX_ALL, nullptr, reinterpret_cast<void**>(&pAudioClientRender));
    if (FAILED(hr)) {
        std::cerr << "Failed to activate audio client!" << std::endl;
        pDevice->Release();
        CoUninitialize();
        return 0;
    }
    WAVEFORMATEX* pFormatRender = nullptr;
    hr = pAudioClientRender->GetMixFormat(&pFormatRender);
    if (FAILED(hr)) {
        std::cerr << "Failed to get mix format!" << std::endl;
        pAudioClientRender->Release();
        pDevice->Release();
        CoUninitialize();
        return 0;
    }
    hr = pAudioClientRender->Initialize(AUDCLNT_SHAREMODE_SHARED, 0, 10000000, 0, pFormatRender, nullptr);
    if (FAILED(hr)) {
        std::cerr << "Failed to initialize audio client!" << std::endl;
        pAudioClient->Release();
        pDevice->Release();
        CoUninitialize();
        return 0;
    }
    IAudioRenderClient* pAudioRender = nullptr;
    hr = pAudioClientRender->GetService(IID_PPV_ARGS(&pAudioRender));
    if (FAILED(hr)) {
        std::cerr << "Failed to get capture client!" << std::endl;
        pAudioClientRender->Release();
        pDevice->Release();
        CoUninitialize();
        return 0;
    }
#pragma endregion

    hr = pAudioClient->Start();
    if (FAILED(hr)) {
        std::cerr << "Failed to start audio capture!" << std::endl;
        pCaptureClient->Release();
        pAudioClient->Release();
        pDevice->Release();
        CoUninitialize();
        return 0;
    }
    hr = pAudioClientRender->Start();
    if (FAILED(hr)) {
        std::cerr << "Failed to start audio render!" << std::endl;
        pCaptureClient->Release();
        pAudioClient->Release();
        pDevice->Release();
        CoUninitialize();
        return 0;
    }
    BYTE* pData = nullptr;
    UINT32 numFramesAvailable;
    DWORD flags;

    std::vector<double> processedData;
    //for (int i = 0; i < 1000; ++i) {  // Пример 100 итераций
    while (true)
    {
    hr = pCaptureClient->GetBuffer(&pData, &numFramesAvailable, &flags, nullptr, nullptr);
        if (FAILED(hr)) {
            std::cerr << "Failed to get buffer!" << std::endl;
            break;
        }
        if (pData != nullptr)
        {
            short* pcmData = reinterpret_cast<short*>(pData);

            processedData.resize(numFramesAvailable);

            // Нормализуем данные в диапазон [-1.0, 1.0]
            for (UINT32 i = 0; i < numFramesAvailable; ++i) {
                processedData[i] = pcmData[i] / 32768.0f; // Преобразуем 16-битные значения в float
            }
            int N = processedData.size();
            fftw_complex* fftData = (fftw_complex*)fftw_malloc(sizeof(fftw_complex) * N);
            fftw_plan planForward = fftw_plan_dft_r2c_1d(N, processedData.data(), fftData, FFTW_ESTIMATE);
            //todo пофиксить баг
            //// Применяем FFT
            fftw_execute(planForward);
            for (int i = 0; i < N / 2 + 1; ++i) {
                float freq = (float)i * pFormat->nSamplesPerSec / N;
                if (freq > 10000) {
                    fftData[i][0] *= 2.5;  // Усиливаем реальную часть
                    fftData[i][1] *= 2.5;  // Усиливаем мнимую часть
                }
            }
            fftw_plan planBackward = fftw_plan_dft_c2r_1d(N, fftData, processedData.data(), FFTW_ESTIMATE);
            fftw_execute(planBackward);

            //// Нормализуем результат
            /*for (int i = 0; i < N; ++i) {
                processedData[i] /= N;
            }*/
            fftw_destroy_plan(planForward);
            fftw_destroy_plan(planBackward);
            fftw_free(fftData);


            // Применение обработки (например, усиление)
            //ProcessAudioData(processedData ,pData, numFramesAvailable, pFormat->nChannels, 1.1f);  // Увеличение громкости на 1.5x
            // Отправляем обработанные данные на вывод
            BYTE* pRenderData = nullptr;
            hr = pAudioRender->GetBuffer(numFramesAvailable, &pRenderData);
            if (FAILED(hr)) {
                std::cerr << "Failed to get render buffer!" << std::endl;
                break;
            }


            short* processedPcmData = reinterpret_cast<short*>(pRenderData);
            // Преобразуем данные обратно в 16-битные целые числа, с учетом нормализации в диапазоне [-1, 1]
            for (size_t i = 0; i < processedData.size(); ++i) {
                processedPcmData[i] = static_cast<short>(std::clamp(processedData[i], -32768.0, 32767.0));
            }


            // Копируем обработанные данные в буфер рендеринга
            //memcpy(pRenderData, processedData.data(), processedData.size());

            // Освобождаем буфер захвата
            hr = pCaptureClient->ReleaseBuffer(numFramesAvailable);
            if (FAILED(hr)) {
                std::cerr << "Failed to release buffer!" << std::endl;
                break;
            }

            // Отправляем буфер на воспроизведение
            hr = pAudioRender->ReleaseBuffer(numFramesAvailable, 0);
            if (FAILED(hr)) {
                std::cerr << "Failed to release render buffer!" << std::endl;
                break;
            }
            processedData.clear();
        }
    }
    pAudioClient->Stop();
    pCaptureClient->Release();
    pAudioClient->Release();
    pDevice->Release();

    // Завершаем COM
    CoUninitialize();
    return 0;
}

 